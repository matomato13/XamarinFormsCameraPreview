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

        private static int _rotationAngle;

        private void SubscribeToOrientationChanges ()
        {
            _orientationNotification = UIDevice.Notifications.ObserveOrientationDidChange((sender, args) => {
                var degrees = 0;
                switch(UIDevice.CurrentDevice.Orientation)
                {
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
                _outputRecorder = new OutputRecorder();
                output.SetSampleBufferDelegate(_outputRecorder, _queue);
                captureSession.AddOutput(output);
            }
        }

        private void Initialize()
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

        private static bool _disable;
        public void Toggle ()
        {
            _disable = !_disable;
        }

        public class OutputRecorder : AVCaptureVideoDataOutputSampleBufferDelegate
        {
            private Size _resizedSize;

            public override void DidOutputSampleBuffer (AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
            {
                try
                {
                    var image = ImageFromSampleBuffer(sampleBuffer);

                    SetPreviewSize(image.Size);

                    var contourImage = ProcessImage(image, _resizedSize);
                    if(contourImage == null)
                    {
                        return;
                    }

                    // Do something with the image, we just stuff it in our main view.
                    ImageView.BeginInvokeOnMainThread(() => {
                        TryDispose(ImageView.Image);
                        ImageView.Image = _disable ? null : contourImage;
                    });
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    sampleBuffer.Dispose();
                }
            }

            private void SetPreviewSize (CGSize size)
            {
                var width = (int)(size.Width / 3);
                var height = (int)(size.Height / 3);

                _resizedSize = new Size(width, height);
            }

            private UIImage ImageFromSampleBuffer (CMSampleBuffer sampleBuffer)
            {
                // Get the CoreVideo image
                using(var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer)
                {
                    // Lock the base address
                    pixelBuffer.Lock(CVPixelBufferLock.None);

                    // Get the number of bytes per row for the pixel buffer
                    var baseAddress = pixelBuffer.BaseAddress;
                    var bytesPerRow = (int)pixelBuffer.BytesPerRow;
                    var width = (int)pixelBuffer.Width;
                    var height = (int)pixelBuffer.Height;
                    var flags = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;

                    // Create a CGImage on the RGB colorspace from the configured parameter above
                    using(var cs = CGColorSpace.CreateDeviceRGB())
                    {
                        using(var context = new CGBitmapContext(baseAddress, width, height, 8, bytesPerRow, cs, (CGImageAlphaInfo)flags))
                        {
                            using(CGImage cgImage = context.ToImage())
                            {
                                pixelBuffer.Unlock(CVPixelBufferLock.None);
                                return UIImage.FromImage(cgImage);
                            }
                        }
                    }
                }
            }

            private void TryDispose (IDisposable obj)
            {
                obj?.Dispose();
            }
        }

        private static bool _busy;
        private static UIImage _lastPreviewFrame;
        private static Point[] _lastContourDetected;

        private static UIImage ProcessImage (UIImage image, Size resizedSize)
        {
            var debug = false;

            if(!_busy)
            {
                try
                {
                    _busy = true;

                    // keep it for later if needed
                    _lastPreviewFrame = image;

                    using(var grey = new Image<Gray, byte>(image))
                    using(var resized = new Image<Gray, byte>(resizedSize))
                    using(var gaussian = new Image<Gray, byte>(resizedSize))
                    using(var canny = new Image<Gray, byte>(resizedSize))
                    {
                        // resize to make image processing faster
                        CvInvoke.Resize(grey, resized, resizedSize);

                        // blur to make edge detection easier
                        CvInvoke.GaussianBlur(resized, gaussian, new Size(5, 5), 0);

                        // canny edge detection
                        CvInvoke.Canny(gaussian, canny, 15, 40);

                        // rotate because the preview image is not rotated, only the surface
                        var rotated = canny.Rotate(_rotationAngle, new Gray(255), false);

                        // helps closing contours
                        var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(5, 5), new Point(1, 1));
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
                                if(angle < 60 || angle > 120)
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