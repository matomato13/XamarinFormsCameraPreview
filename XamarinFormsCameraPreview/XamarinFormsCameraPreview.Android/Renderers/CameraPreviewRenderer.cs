using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using ApxLabs.FastAndroidCamera;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using XamarinFormsCameraPreview.Views;
using Camera = Android.Hardware.Camera;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using View = Android.Views.View;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(XamarinFormsCameraPreview.Droid.Renderers.CameraPreviewRenderer))]

namespace XamarinFormsCameraPreview.Droid.Renderers
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, FrameLayout>, ISurfaceHolderCallback, INonMarshalingPreviewCallback
    {
		private Camera _camera;
	    private FrameLayout _frameLayout;

        private bool _busy;
        private Point[] _lastContourDetected;
        private byte[] _lastPreviewFrame;
        private ImageView _overlay;
        private Size _resizedSize;
        private Size _previewSize;
        private SurfaceOrientation _deviceOrientation;
        private Camera.CameraInfo _cameraInfo;
        private FastJavaByteArray _buffer;
        private IList<FastJavaByteArray> _buffers = new List<FastJavaByteArray>();

        protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
		{
			base.OnElementChanged(e);

		    if (e.OldElement == null)
		    {
		        // get CameraPreview object and set event handler
		        var preview = e.NewElement;
		        preview.PictureRequired += OnPictureRequired;

		        // create and set surface view
		        var surfaceView = new SurfaceView(Context);
		        surfaceView.Holder.AddCallback(this);

                _frameLayout = new FrameLayout(Context);
		        _overlay = new ImageView(Context);
                _overlay.SetScaleType(ImageView.ScaleType.FitXy);
                _frameLayout.AddView(surfaceView);
                _frameLayout.AddView(_overlay);

		        SetNativeControl(_frameLayout);
		    }
		}

		private void OnPictureRequired(object sender, EventArgs e)
		{
			var preview = sender as CameraPreview;
			if (_camera != null && preview != null)
            {
				_camera.TakePicture(null, null, new DelegatePictureCallback
                {
					PictureTaken = (data, camera) =>
					{
					    var getColoredResult = true;

                        if (_lastPreviewFrame == null
                            || _lastPreviewFrame.Length == 0
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

                            var handle = GCHandle.Alloc(_lastPreviewFrame, GCHandleType.Pinned);
                            using (var yuv420sp = new Image<Gray, byte>(width, (height >> 1) * 3, width, handle.AddrOfPinnedObject()))
                            using (var bgr = new Image<Bgr, byte>(width, height))
                            {
                                CvInvoke.CvtColor(yuv420sp, bgr, ColorConversion.Yuv420Sp2Bgr);

                                var image = bgr.Rotate(CameraHelper.GetRotationAngle(_deviceOrientation, _cameraInfo), new Bgr(255, 255, 255), false);
                                var rotatedSize = image.Size.Width > image.Size.Height
                                    ? _resizedSize
                                    : new Size(_resizedSize.Height, _resizedSize.Width);

                                var orderedPoints = GeometryHelper.ToScaledPointFArray(_lastContourDetected, image.Size, rotatedSize);
                                var warped = GeometryHelper.FourPointTransform(image, orderedPoints);

                                if (!getColoredResult)
                                {
                                    // convert to grayscale
                                    var gray = warped.Convert<Gray, byte>();

                                    // cleanup the image (if needed, this needs to be improved)
                                    CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, Emgu.CV.CvEnum.ThresholdType.Binary, 11, 2);

                                    ProposeResultToUser(gray);
                                }
                                else
                                {
                                    ProposeResultToUser(warped);
                                }
                            }

                            handle.Free();
                            _lastPreviewFrame = null;
                            _lastContourDetected = null;
                        }
                        finally
                        {
                            _busy = false;
                        }
                    }
				});
			}
		}

        private void ProposeResultToUser<TColor>(Image<TColor, byte> image)
            where TColor : struct, IColor
        {
            SaveImage(image.Bitmap, "result.png");
        }
        
        public void OnPreviewFrame(IntPtr data, Camera camera)
	    {
            var debug = false;

            if (!_busy)
            {
                try
                {
                    _busy = true;

                    if (_buffer != null)
                    {
                        _buffer?.Dispose();
                        _buffer = null;
                    }

                    _buffer = new FastJavaByteArray(data);

                    // keep it for later if needed
                    var bytes = new byte[_buffer.Count];
                    _buffer.CopyTo(bytes, 0);
                    _lastPreviewFrame = (byte[])bytes.Clone();

                    _resizedSize = new Size((int)(_previewSize.Width / 1.5), (int)(_previewSize.Height / 1.5));

                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    using (var grey = new Image<Gray, byte>(_previewSize.Width, _previewSize.Height, _previewSize.Width, handle.AddrOfPinnedObject()))
                    using (var resized = new Image<Gray, byte>(_resizedSize))
                    using (var gaussian = new Image<Gray, byte>(_resizedSize))
                    using (var canny = new Image<Gray, byte>(_resizedSize))
                    {
                        // resize to make image processing faster
                        CvInvoke.Resize(grey, resized, _resizedSize);

                        // blur to make edge detection easier
                        CvInvoke.GaussianBlur(resized, gaussian, new Size(5, 5), 0);

                        // canny edge detection
                        CvInvoke.Canny(gaussian, canny, 15, 40);

                        var rotated = canny.Rotate(CameraHelper.GetRotationAngle(_deviceOrientation, _cameraInfo), new Gray(255), false);

                        // test stuff
                        var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(5, 5), new Point(1, 1));
                        CvInvoke.MorphologyEx(rotated, rotated, MorphOp.Close, element, new Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
                        //CvInvoke.Dilate(canny, canny, null, new System.Drawing.Point(-1, -1), 3, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

                        //_overlay.SetImageBitmap(canny.Bitmap);
                        //camera.AddCallbackBuffer(data);
                        //return;
                        //

                        // find contours
                        var contoursDetected = new VectorOfVectorOfPoint();
                        CvInvoke.FindContours(rotated, contoursDetected, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

                        // get more info on contours
                        var approxContours = contoursDetected.ToArrayOfArray().Select(x =>
                        {
                            var contour = new VectorOfPoint(x);
                            var approxContour = new VectorOfPoint();
                            CvInvoke.ApproxPolyDP(contour, approxContour, CvInvoke.ArcLength(contour, true) * 0.05, true);

                            var isRectangle = true;
                            var edges = PointCollection.PolyLine(approxContour.ToArray(), true);

                            for (var j = 0; j < edges.Length; j++)
                            {
                                var angle = Math.Abs(edges[(j + 1) % edges.Length].GetExteriorAngleDegree(edges[j]));
                                if (angle < 50 || angle > 130)
                                {
                                    isRectangle = false;
                                    break;
                                }
                            }

                            return new
                            {
                                Contour = contour,
                                ApproxContour = approxContour,
                                ApproxArea = CvInvoke.ContourArea(approxContour),
                                ApproxIsRectangle = isRectangle
                            };
                        });

                        // find biggest rectangle
                        var biggestContour = approxContours.Where(x => x.ApproxContour.Size == 4 && x.ApproxIsRectangle)
                            .OrderByDescending(x => x.ApproxArea).FirstOrDefault();

                        using (var bgrContour = new Image<Bgra, byte>(rotated.Size))
                        {
                            if (debug)
                            {
                                // all detected
                                CvInvoke.DrawContours(bgrContour, contoursDetected, -1, new MCvScalar(0, 0, 255), 3); //red

                                // only fitting contours
                                var fittingContours =  approxContours.Where(x => x.ApproxContour.Size == 4 && x.ApproxIsRectangle)
                                    .OrderByDescending(x => x.ApproxArea).ToList();
                                var fittingContoursVector = new VectorOfVectorOfPoint(fittingContours.Count);
                                foreach (var fittingContour in fittingContours)
                                {
                                    fittingContoursVector.Push(fittingContour.ApproxContour);
                                }

                                CvInvoke.DrawContours(bgrContour, fittingContoursVector, -1, new MCvScalar(255, 0, 0), 3); //blue
                            }

                            if (biggestContour != null)
                            {
                                CvInvoke.DrawContours(bgrContour, new VectorOfVectorOfPoint(biggestContour.ApproxContour), -1, new MCvScalar(0, 255, 0), 3); //green
                                _lastContourDetected = biggestContour.ApproxContour.ToArray();
                            }

                            _bitmap?.Recycle();
                            _bitmap = bgrContour.ToBitmap();
                            _overlay.SetImageBitmap(_bitmap);
                        }

                        rotated.Dispose();
                    }

                    handle.Free();

                    Invalidate();
                }
                finally
                {
                    if (_bitmap != null)
                    {
                        _bitmap.Dispose();
                        _bitmap = null;
                    }
                    
                    _busy = false;
                }
            }
            camera.AddCallbackBuffer(_buffer);
        }

        private Bitmap _bitmap;

        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {
            if (_camera != null)
            {
                try
                {
                    _camera.StopPreview();
                }
                catch
                {
                    // nothing to catch, we tried to stop a preview that didn't exist
                }

                _deviceOrientation = Context.GetSystemService(Context.WindowService)
                    .JavaCast<IWindowManager>()
                    .DefaultDisplay.Rotation;

                _cameraInfo = CameraHelper.GetCameraInfo();

                _previewSize = CameraHelper.SetCameraParameters(_deviceOrientation, _cameraInfo, _camera, width, height, _buffers);

                _camera.SetPreviewDisplay(holder);
                _camera.StartPreview();
            }
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            try
            {
                _camera = Camera.Open();
            }
            catch
            {
                _camera = null;
            }

            if (_camera != null)
            {
                _camera.SetNonMarshalingPreviewCallback(this);
                _camera.SetPreviewDisplay(holder);
            }
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            if (_camera != null)
            {
                _camera.SetNonMarshalingPreviewCallback(null);
                _camera.StopPreview();
                _camera.Release();
                _camera.Dispose();
                _camera = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var buffer in _buffers)
                {
                    buffer?.Dispose();
                }

                _buffers = new List<FastJavaByteArray>();

                _buffer?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void SaveImage(Bitmap bitmap, string name)
        {
            var sdCardPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            var filePath = System.IO.Path.Combine(sdCardPath, name);
            var stream = new FileStream(filePath, FileMode.Create);
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            stream.Close();
        }
    }
}