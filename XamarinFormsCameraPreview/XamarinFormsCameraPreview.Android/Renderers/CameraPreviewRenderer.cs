using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Android.Content;
using Android.Graphics;
using Android.Runtime;
using Android.Views;
using Android.Hardware;
using Android.Widget;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using XamarinFormsCameraPreview.Views;
using Camera = Android.Hardware.Camera;
using PointF = System.Drawing.PointF;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(XamarinFormsCameraPreview.Droid.Renderers.CameraPreviewRenderer))]

namespace XamarinFormsCameraPreview.Droid.Renderers
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, FrameLayout>, ISurfaceHolderCallback, Camera.IPreviewCallback
	{
		private Camera _camera;
	    private FrameLayout _frameLayout;

		protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
		{
			base.OnElementChanged(e);

		    if (e.OldElement == null)
		    {
		        // get CameraPreview object and set event handler
		        var preview = e.NewElement;
		        preview.PictureRequired += preview_PictureRequired;

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

	    /// <summary>
		/// called when the picture is required
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void preview_PictureRequired(object sender, EventArgs e)
		{
			CameraPreview preview = sender as CameraPreview;
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

                            var pictureSize = _camera.GetParameters().PreviewSize;

                            var handle = GCHandle.Alloc(_lastPreviewFrame, GCHandleType.Pinned);
                            using (var yuv420sp = new Image<Gray, byte>(pictureSize.Width, (pictureSize.Height >> 1) * 3, pictureSize.Width, handle.AddrOfPinnedObject()))
                            using (var bgr = new Image<Bgr, byte>(pictureSize.Width, pictureSize.Height))
                            {
                                CvInvoke.CvtColor(yuv420sp, bgr, ColorConversion.Yuv420Sp2Bgr);

                                var image = bgr.Rotate(GetRotationAngle(), new Bgr(255, 255, 255), false);

                                var orderedPoints = ToScaledPointFArray(_lastContourDetected, new System.Drawing.Size(image.Width, image.Height));
                                var warped = FourPointTransform(image, orderedPoints);

                                if (!getColoredResult)
                                {
                                    var gray = warped.Convert<Gray, byte>();
                                    CvInvoke.AdaptiveThreshold(gray, gray, 255, AdaptiveThresholdType.GaussianC, Emgu.CV.CvEnum.ThresholdType.Binary, 11, 2);

                                    SaveImage(gray.Bitmap, "gray.png");
                                }

                                SaveImage(warped.Bitmap, "warped.png");
                            }

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

        private void SaveImage(Bitmap bitmap, string name)
        {
            var sdCardPath = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            var filePath = System.IO.Path.Combine(sdCardPath, name);
            var stream = new FileStream(filePath, FileMode.Create);
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
            stream.Close();
        }

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

                SetCameraParameters(width, height);

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
                _camera.SetPreviewCallback(this); // TODO vs SetPreviewCallbackWithBuffer?
                _camera.SetPreviewDisplay(holder);
			}
		}

		public void SurfaceDestroyed(ISurfaceHolder holder)
		{
			if (_camera != null)
            {
                _camera.SetPreviewCallback(null);
				_camera.StopPreview();
				_camera.Release();
				_camera.Dispose();
				_camera = null;
			}
		}

        private bool _busy;
        private byte[] _bgrData;
        private System.Drawing.Point[] _lastContourDetected { get; set; }
        private byte[] _lastPreviewFrame { get; set; }
        private ImageView _overlay;
        private System.Drawing.Size _resizedSize;

        public void OnPreviewFrame(byte[] data, Camera camera)
	    {
            var debug = false;

            if (!_busy)
            {
                try
                {
                    _busy = true;

                    // keep it for later if needed
                    _lastPreviewFrame = (byte[])data.Clone();

                    var previewSize = camera.GetParameters().PreviewSize;
                    System.Drawing.Size newSize;
                    data = RotateCounterClockwise(data, new System.Drawing.Size(previewSize.Width, previewSize.Height), out newSize);

                    var handle = GCHandle.Alloc(data, GCHandleType.Pinned);

                    if (_bgrData == null || _bgrData.Length < newSize.Width * newSize.Height)
                    {
                        _bgrData = new byte[newSize.Width * newSize.Height * 3];
                    }
                    
                    _resizedSize = new System.Drawing.Size((int)(newSize.Width / 1.5), (int)(newSize.Height / 1.5));

                    var bgrHandle = GCHandle.Alloc(_bgrData, GCHandleType.Pinned);
                    using (var grey = new Image<Gray, byte>(newSize.Width, newSize.Height, newSize.Width, handle.AddrOfPinnedObject()))
                    using (var resized = new Image<Gray, byte>(_resizedSize))
                    using (var gaussian = new Image<Gray, byte>(_resizedSize))
                    using (var canny = new Image<Gray, byte>(_resizedSize.Width, _resizedSize.Height, _resizedSize.Width, bgrHandle.AddrOfPinnedObject()))
                    {
                        // resize to make image processing faster
                        CvInvoke.Resize(grey, resized, _resizedSize);

                        // blur to make edge detection easier
                        CvInvoke.GaussianBlur(resized, gaussian, new System.Drawing.Size(5, 5), 0);

                        // canny edge detection
                        CvInvoke.Canny(gaussian, canny, 15, 40);

                        // test stuff
                        var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new System.Drawing.Size(5, 5), new System.Drawing.Point(1, 1));
                        CvInvoke.MorphologyEx(canny, canny, MorphOp.Close, element, new System.Drawing.Point(-1, -1), 1, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);
                        //CvInvoke.Dilate(canny, canny, null, new System.Drawing.Point(-1, -1), 3, BorderType.Constant, CvInvoke.MorphologyDefaultBorderValue);

                        //_overlay.SetImageBitmap(canny.Bitmap);
                        //camera.AddCallbackBuffer(data);
                        //return;
                        //

                        // find contours
                        var contoursDetected = new VectorOfVectorOfPoint();
                        CvInvoke.FindContours(canny, contoursDetected, null, RetrType.List, ChainApproxMethod.ChainApproxSimple);

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

                        using (var bgrContour = new Image<Bgra, byte>(_resizedSize))
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

                            _overlay.SetImageBitmap(bgrContour.Bitmap);
                        }
                    }

                    handle.Free();

                    Invalidate();
                }
                finally
                {
                    _busy = false;
                }
            }
            camera.AddCallbackBuffer(data);
        }

        private byte[] RotateCounterClockwise(byte[] data, System.Drawing.Size originalSize, out System.Drawing.Size newSize)
        {
            var rotate = false;
            newSize = new System.Drawing.Size(originalSize.Width, originalSize.Height);

            var cDegrees = GetRotationAngle();

            if (cDegrees == 90 || cDegrees == 270)
            {
                rotate = true;
                newSize.Width = originalSize.Height;
                newSize.Height = originalSize.Width;
            }

            if (!rotate)
            {
                return data;
            }

            var rotatedData = new byte[data.Length];
            for (var y = 0; y < originalSize.Height; y++)
            {
                for (var x = 0; x < originalSize.Width; x++)
                {
                    rotatedData[x * originalSize.Height + originalSize.Height - y - 1] = data[x + y * originalSize.Width];
                }
            }
            return rotatedData;
        }

        private int GetRotationAngle()
        {
            var degrees = 0;
            switch (Context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>().DefaultDisplay.Rotation)
            {
                case SurfaceOrientation.Rotation0:
                    break;
                case SurfaceOrientation.Rotation90:
                    degrees = 90;
                    break;
                case SurfaceOrientation.Rotation180:
                    degrees = 180;
                    break;
                case SurfaceOrientation.Rotation270:
                    degrees = 270;
                    break;
            }

            return (GetCameraInfo().Orientation - degrees + 360) % 360;
        }

        private Camera.CameraInfo GetCameraInfo()
        {
            // Find the total number of cameras available
            var numberOfCameras = Camera.NumberOfCameras;

            // Find the ID of the default camera
            var cameraInfo = new Camera.CameraInfo();
            for (var i = 0; i < numberOfCameras; i++)
            {
                Camera.GetCameraInfo(i, cameraInfo);
                if (cameraInfo.Facing == CameraFacing.Back)
                {
                    return cameraInfo;
                }
            }

            return cameraInfo;
        }

        private void SetCameraParameters(int width, int height)
        {
            var parameters = _camera.GetParameters();

            // Set AutoFocus
            if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            else if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousVideo))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousVideo;
            else
                parameters.FocusMode = Camera.Parameters.FocusModeAuto;

            var rotationAngle = GetRotationAngle();
            var isPortrait = rotationAngle == 90 || rotationAngle == 270;

            // Set Preview and PictureSize to the same values
            var previewSize = GetOptimalPreviewSize(parameters.SupportedPreviewSizes, isPortrait ? height : width, isPortrait ? width: height);
            var pictureSize = GetOptimalPictureSize(previewSize, parameters.SupportedPictureSizes);

            parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
            parameters.SetPictureSize(pictureSize.Width, pictureSize.Height);

            // Rotate preview and final picture
            _camera.SetDisplayOrientation(rotationAngle);
            parameters.SetRotation(rotationAngle);

            _camera.SetParameters(parameters);
        }

        private Camera.Size GetOptimalPreviewSize(IList<Camera.Size> sizes, int w, int h)
        {
            const double ASPECT_TOLERANCE = 0.1;
            var targetRatio = (double)w / h;

            if (sizes == null)
            {
                return null;
            }

            Camera.Size optimalSize = null;
            var minDiff = double.MaxValue;

            var targetHeight = h;

            // Try to find an size match aspect ratio and size
            foreach (var size in sizes)
            {
                double ratio = (double)size.Width / size.Height;

                if (Math.Abs(ratio - targetRatio) > ASPECT_TOLERANCE)
                {
                    continue;
                }

                if (Math.Abs(size.Height - targetHeight) < minDiff)
                {
                    optimalSize = size;
                    minDiff = Math.Abs(size.Height - targetHeight);
                }
            }

            // Cannot find the one match the aspect ratio, ignore the requirement
            if (optimalSize == null)
            {
                minDiff = double.MaxValue;
                foreach (var size in sizes)
                {
                    if (Math.Abs(size.Height - targetHeight) < minDiff)
                    {
                        optimalSize = size;
                        minDiff = Math.Abs(size.Height - targetHeight);
                    }
                }
            }

            return optimalSize;
        }

        private Camera.Size GetOptimalPictureSize(Camera.Size previewSize, IList<Camera.Size> sizes)
        {
            const double ASPECT_TOLERANCE = 0.12;
            var targetRatio = (double)previewSize.Width / previewSize.Height;

            if (sizes == null)
            {
                return null;
            }

            Camera.Size optimalSize = null;
            var minDiff = double.MaxValue;

            var targetHeight = previewSize.Height;

            // Try to find an size match aspect ratio and size
            foreach (var size in sizes)
            {
                double ratio = (double)size.Width / size.Height;

                if (Math.Abs(ratio - targetRatio) > ASPECT_TOLERANCE)
                {
                    continue;
                }

                //return size;
                if (Math.Abs(size.Height - targetHeight) < minDiff)
                {
                    optimalSize = size;
                    minDiff = Math.Abs(size.Height - targetHeight);
                }
            }

            // Cannot find the one match the aspect ratio, ignore the requirement
            if (optimalSize == null)
            {
                minDiff = double.MaxValue;
                foreach (var size in sizes)
                {
                    if (Math.Abs(size.Height - targetHeight) < minDiff)
                    {
                        optimalSize = size;
                        minDiff = Math.Abs(size.Height - targetHeight);
                    }
                }
            }

            return optimalSize;
        }

        private PointF[] ToScaledPointFArray(System.Drawing.Point[] contour, System.Drawing.Size pictureSize)
        {
            // extrapolation of preview contour to fullsize picture
            var xScale = (float)pictureSize.Width / _resizedSize.Width;
            var yScale = (float)pictureSize.Height / _resizedSize.Height;

            // they should be ordered as topleft, topright, bottomright, bottomleft
            var orderedPoints = new PointF[4];

            var leftPoints = contour.OrderBy(x => x.X).Take(2).ToList();
            var rightPoints = contour.OrderByDescending(x => x.X).Take(2).ToList();

            orderedPoints[0] = leftPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).First();
            orderedPoints[1] = rightPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).First();
            orderedPoints[2] = rightPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).Last();
            orderedPoints[3] = leftPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).Last();

            return orderedPoints;
        }

        private Image<TColor, byte> FourPointTransform<TColor>(Image<TColor, byte> grey, PointF[] orderedPoints)
            where TColor : struct, IColor
        {
            // obtain a consistent order of the points and unpack them individually
            var tl = orderedPoints[0];
            var tr = orderedPoints[1];
            var br = orderedPoints[2];
            var bl = orderedPoints[3];

            // compute the width of the new image, which will be the maximum distance between bottom-right and bottom-left
            // x-coordiates or the top-right and top-left x-coordinates
            var widthA = Math.Sqrt(Math.Pow(br.X - bl.X, 2) + Math.Pow(br.Y - bl.Y, 2));
            var widthB = Math.Sqrt(Math.Pow(tr.X - tl.X, 2) + Math.Pow(tr.Y - tl.Y, 2));
            var maxWidth = Math.Max(widthA, widthB);

            // compute the height of the new image, which will be the maximum distance between the top-right and bottom-right
            // y-coordinates or the top-left and bottom-left y-coordinates
            var heightA = Math.Sqrt(Math.Pow(tr.X - br.X, 2) + Math.Pow(tr.Y - br.Y, 2));
            var heightB = Math.Sqrt(Math.Pow(tl.X - bl.X, 2) + Math.Pow(tl.Y - bl.Y, 2));
            var maxHeight = Math.Max(heightA, heightB);

            // now that we have the dimensions of the new image, construct the set of destination points to obtain a "birds eye view",
            // (i.e. top-down view) of the image, again specifying points in the top-left, top-right, bottom-right, and bottom-left order
            var newSize = new System.Drawing.Size((int)maxWidth, (int)maxHeight);
            var dst = new[]
            {
                new PointF(0, 0), new PointF(newSize.Width - 1, 0), new PointF(newSize.Width - 1, newSize.Height - 1), new PointF(0, newSize.Height - 1)
            };

            Image<TColor, byte> result;

            // compute the perspective transform matrix and then apply it
            using (var matrix = CvInvoke.GetPerspectiveTransform(orderedPoints, dst))
            {
                using (var warped = new Image<TColor, byte>(newSize))
                {
                    CvInvoke.WarpPerspective(grey, warped, matrix, newSize);
                    result = warped.Copy();
                }
            }

            return result;
        }
    }
}