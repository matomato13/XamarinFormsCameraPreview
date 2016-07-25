using System;
using System.Diagnostics;
using System.Linq;
using AVFoundation;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Foundation;
using UIKit;
using Xamarin.Forms;
using XamarinFormsCameraPreview.Helpers;
using XamarinFormsCameraPreview.Views;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace XamarinFormsCameraPreview.iOS.Renderers
{
    [Register("CameraPreviewView")]
    public class CameraPreviewView : UIView
    {
        private NSObject _orientationNotification;
        private AVCaptureVideoPreviewLayer _previewLayer;
        private OutputRecorder _outputRecorder;
        private DispatchQueue _queue;
        private bool _busy;
        private UIImage _lastPreviewFrame;
        private Point[] _lastContourDetected;
        private Size _resizedSize;
        private Size _previewSize;
        private static int _rotationAngle;
        private const double TargetPixelArea = 853 * 512; // the image processing works best with this resolution, we resize the image to have something similar

        public static UIImageView ImageView;

        public CameraPreviewView()
        {
            Initialize();
        }

        public CameraPreviewView(CGRect bounds)
            : base(bounds)
        {
            Initialize();
        }

        public override void Draw (CGRect rect)
        {
            base.Draw(rect);
            _previewLayer.Frame = Bounds;
        }

        private void Initialize ()
        {
            BackgroundColor = UIColor.Black;

            SubscribeToOrientationChanges();

            var device = AVCaptureDevice.DefaultDeviceWithMediaType(AVMediaType.Video);
            if(device == null)
            {
                Debug.WriteLine("No device detected.");
                return;
            }

            var captureSession = new AVCaptureSession();

            ConnectCamera(captureSession, device);
            ConnectPreview(captureSession);
            ConnectOutput(captureSession);

            captureSession.StartRunning();

            ImageView = new UIImageView();
            ImageView.TranslatesAutoresizingMaskIntoConstraints = false;
            ImageView.ContentMode = UIViewContentMode.ScaleToFill;
            AddSubview(ImageView);

            AddConstraints(new NSLayoutConstraint[]{
                NSLayoutConstraint.Create(ImageView, NSLayoutAttribute.Leading, NSLayoutRelation.Equal, ImageView.Superview, NSLayoutAttribute.Leading, 1, 0),
                NSLayoutConstraint.Create(ImageView, NSLayoutAttribute.Trailing, NSLayoutRelation.Equal, ImageView.Superview, NSLayoutAttribute.Trailing, 1, 0),
                NSLayoutConstraint.Create(ImageView, NSLayoutAttribute.Top, NSLayoutRelation.Equal, ImageView.Superview, NSLayoutAttribute.Top, 1, 0),
                NSLayoutConstraint.Create(ImageView, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, ImageView.Superview, NSLayoutAttribute.Bottom, 1, 0)
            });
        }

        private UIImage ProcessImage (UIImage image)
        {
            var debug = false;

            if(!_busy)
            {
                try
                {
                    _busy = true;

                    // keep it for later if needed
                    _lastPreviewFrame = image;

                    SetPreviewSize(image.Size);

                    using(var grey = new Image<Gray, byte>(image))
                    using(var resized = new Image<Gray, byte>(_resizedSize))
                    using(var gaussian = new Image<Gray, byte>(_resizedSize))
                    using(var canny = new Image<Gray, byte>(_resizedSize))
                    {
                        // resize to make image processing faster
                        CvInvoke.Resize(grey, resized, _resizedSize);

                        // blur to make edge detection easier
                        CvInvoke.GaussianBlur(resized, gaussian, new Size(5, 5), 0);

                        // canny edge detection
                        CvInvoke.Canny(gaussian, canny, 15, 40);

                        // rotate because the preview image is not rotated, only the surface
                        var rotated = canny.Rotate(_rotationAngle, new Gray(255), false);

                        // helps closing contours
                        var morphSize = 1;
                        var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(2*morphSize+1, 2*morphSize+1), new Point(morphSize, morphSize));
                        CvInvoke.MorphologyEx(rotated, rotated, MorphOp.Close, element, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

                        // find contours
                        var contoursDetected = new VectorOfVectorOfPoint();
                        CvInvoke.FindContours(rotated, contoursDetected, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

                        // get more info on contours
                        var approxContours = contoursDetected.ToArrayOfArray().Select(x => {
                            var contour = new VectorOfPoint(x);
                            var approxContour = new VectorOfPoint();
                            CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.05, true);

                            var isRectangle = true;
                            var edges = PointCollection.PolyLine(approxContour.ToArray(), true);

                            for(var j = 0; j < edges.Length; j++)
                            {
                                var angle = Math.Abs(edges[(j + 1) % edges.Length].GetExteriorAngleDegree(edges[j]));
                                if(angle < 75 || angle > 105)
                                {
                                    isRectangle = false;
                                    break;
                                }
                            }

                            return new {
                                Contour = contour,
                                ApproxContour = approxContour,
                                ApproxArea = CvInvoke.ContourArea(approxContour),
                                ApproxIsRectangle = isRectangle
                            };
                        });

                        // find biggest rectangle
                        var biggestContour = approxContours.Where(x => x.ApproxContour.Size == 4 && x.ApproxIsRectangle)
                            .OrderByDescending(x => x.ApproxArea).FirstOrDefault();

                        using(var bgrContour = new Image<Bgra, byte>(rotated.Size))
                        {
                            if(debug)
                            {
                                // all detected
                                CvInvoke.DrawContours(bgrContour, contoursDetected, -1, new MCvScalar(0, 0, 255), 3); //red

                                // only fitting contours
                                var fittingContours = approxContours.Where(x => x.ApproxContour.Size == 4 && x.ApproxIsRectangle)
                                    .OrderByDescending(x => x.ApproxArea).ToList();
                                var fittingContoursVector = new VectorOfVectorOfPoint(fittingContours.Count);
                                foreach(var fittingContour in fittingContours)
                                {
                                    fittingContoursVector.Push(fittingContour.ApproxContour);
                                }

                                CvInvoke.DrawContours(bgrContour, fittingContoursVector, -1, new MCvScalar(255, 0, 0), 3); //blue
                            }

                            if(biggestContour != null)
                            {
                                CvInvoke.DrawContours(bgrContour, new VectorOfVectorOfPoint(biggestContour.ApproxContour), -1, new MCvScalar(0, 255, 0), 3); //green
                                _lastContourDetected = biggestContour.ApproxContour.ToArray();
                            }

                            return bgrContour.ToUIImage();
                        }
                    }
                }
                finally
                {
                    _busy = false;
                }
            }

            return null;
        }

        public void OnPictureRequired (object sender, EventArgs e)
        {
            var preview = sender as CameraPreview;
            if(preview != null)
            {
                var getColoredResult = true;

                if(_lastPreviewFrame == null
                    || _lastContourDetected == null
                    || _lastContourDetected.Length == 0)
                {
                    return;
                }

                try
                {
                    _busy = true;

                    var width = _previewSize.Width;
                    var height = _previewSize.Height;

                    using(var bgr = new Image<Bgr, byte>(_lastPreviewFrame))
                    {
                        var image = bgr.Rotate(_rotationAngle, new Bgr(255, 255, 255), false);
                        var rotatedSize = image.Size.Width > image.Size.Height
                            ? _resizedSize
                            : new Size(_resizedSize.Height, _resizedSize.Width);

                        var orderedPoints = GeometryHelper.ToScaledPointFArray(_lastContourDetected, image.Size, rotatedSize);
                        var warped = GeometryHelper.FourPointTransform(image, orderedPoints);

                        if(!getColoredResult)
                        {
                            // convert to grayscale
                            var gray = warped.Convert<Gray, byte>();

                            // cleanup the image (if needed, this needs to be improved)
                            CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, ThresholdType.Binary, 11, 2);

                            ProposeResultToUser(preview, gray);
                        }
                        else
                        {
                            ProposeResultToUser(preview, warped);
                        }
                    }

                    _lastPreviewFrame = null;
                    _lastContourDetected = null;
                }
                finally
                {
                    _busy = false;
                }
            }
        }

        private void ProposeResultToUser<TColor> (CameraPreview preview, Image<TColor, byte> image)
            where TColor : struct, IColor
        {
            var uiImage = image.ToUIImage();
            //SaveImage(uiImage, "result.png");

            preview.OnPictureTaken(new Models.Image(
                ImageSource.FromStream(() => uiImage.AsPNG().AsStream())
            ));
        }

        private void SubscribeToOrientationChanges ()
        {
            _orientationNotification = UIDevice.Notifications.ObserveOrientationDidChange((sender, args) => {
                var degrees = 0;
                switch(UIDevice.CurrentDevice.Orientation)
                {
                    case UIDeviceOrientation.FaceDown:
                    case UIDeviceOrientation.FaceUp:
                        return;
                    case UIDeviceOrientation.PortraitUpsideDown:
                        degrees = 180;
                        _previewLayer.Connection.VideoOrientation = AVCaptureVideoOrientation.PortraitUpsideDown;
                        break;
                    case UIDeviceOrientation.LandscapeLeft:
                        degrees = 90;
                        _previewLayer.Connection.VideoOrientation = AVCaptureVideoOrientation.LandscapeRight;
                        break;
                    case UIDeviceOrientation.LandscapeRight:
                        degrees = 270;
                        _previewLayer.Connection.VideoOrientation = AVCaptureVideoOrientation.LandscapeLeft;
                        break;
                    default:
                        degrees = 0;
                        _previewLayer.Connection.VideoOrientation = AVCaptureVideoOrientation.Portrait;
                        break;
                }
                _rotationAngle = (90 - degrees + 360) % 360;
                _previewLayer.Frame = Bounds;
            });
        }

        private void ConnectCamera (AVCaptureSession captureSession, AVCaptureDevice device)
        {
            captureSession.SessionPreset = AVCaptureSession.PresetHigh;

            NSError error;

            var input = new AVCaptureDeviceInput(device, out error);
            captureSession.AddInput(input);
        }

        private void ConnectPreview (AVCaptureSession captureSession)
        {
            _previewLayer = new AVCaptureVideoPreviewLayer(captureSession) {
                VideoGravity = AVLayerVideoGravity.ResizeAspectFill,
                Frame = Bounds
            };
            Layer.AddSublayer(_previewLayer);
        }

        private void ConnectOutput (AVCaptureSession captureSession)
        {
            var settings = new CVPixelBufferAttributes { PixelFormatType = CVPixelFormatType.CV32BGRA };

            // create a VideoDataOutput and add it to the session
            using(var output = new AVCaptureVideoDataOutput { WeakVideoSettings = settings.Dictionary })
            {
                _queue = new DispatchQueue("myQueue");
                _outputRecorder = new OutputRecorder(image => ProcessImage(image));
                output.SetSampleBufferDelegate(_outputRecorder, _queue);
                captureSession.AddOutput(output);
            }
        }

        private void SetPreviewSize (CGSize size)
        {
            if(_previewSize.Width != size.Width)
            {
                _previewSize = new Size((int)size.Width, (int)size.Height);

                var resizeRatio = GetResizeRatio(_previewSize);
                _resizedSize = new Size((int)(_previewSize.Width / resizeRatio), (int)(_previewSize.Height / resizeRatio));
            }
        }

        private double GetResizeRatio (Size previewSize)
        {
            double previewPixelRatio = previewSize.Width * previewSize.Height;
            return Math.Round(Math.Sqrt(previewPixelRatio / TargetPixelArea), 3);
        }

        protected override void Dispose (bool disposing)
        {
            if(disposing)
            {
                _orientationNotification.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}