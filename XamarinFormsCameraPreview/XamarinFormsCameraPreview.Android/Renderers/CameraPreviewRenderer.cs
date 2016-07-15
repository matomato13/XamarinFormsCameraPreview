using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Hardware;

using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

using XamarinFormsCameraPreview.Views;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(XamarinFormsCameraPreview.Droid.Renderers.CameraPreviewRenderer))]

namespace XamarinFormsCameraPreview.Droid.Renderers
{
	// To use generic version of the ViewRenderer, Xamarin.Forms have to be updated to 1.2.2.*
	public class CameraPreviewRenderer : ViewRenderer<CameraPreview, SurfaceView>, ISurfaceHolderCallback, Camera.IPreviewCallback
	{
		Camera _camera = null;

		protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
		{
			base.OnElementChanged(e);

			if (e.OldElement == null) {
				// get CamerPreview object and set event handler
				var preview = e.NewElement;
				preview.PictureRequired += preview_PictureRequired;

				// create and set surface view
				var surfaceView = new SurfaceView(Context);
				surfaceView.Holder.AddCallback(this);
				SetNativeControl(surfaceView);
			}
		}

		/// <summary>
		/// called when the picture is required
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void preview_PictureRequired(object sender, EventArgs e)
		{
			CameraPreview preview = sender as CameraPreview;
			if (_camera != null && preview != null) {
				_camera.TakePicture(null, null, new DelegatePictureCallback {
					PictureTaken = (data, camera) => 
                    {
                        // Needed if you want to restart the preview immediately because TakePicture stops the preview
                        _camera.StartPreview();

                        // write jpeg data into a memory stream
                        MemoryStream ms = null;
						try {
							ms = new MemoryStream(data.Length);
							ms.Write(data, 0, data.Length);
							ms.Flush();
							ms.Seek(0, SeekOrigin.Begin);

							// load image source from stream
							preview.OnPictureTaken(new Models.AndroidImage {
								ImageSource = ImageSource.FromStream(() => ms)
							});

							// NOTE: Do not dispose memory stream if it succeeded.
							// ImageSource is loaded in background so ms should not be disposed immediately.
						}
						catch {
							if (ms != null) {
								ms.Dispose();
							}
							throw;
						}
					}
				});
			}
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

	        var cameraInfo = GetCameraInfo();

            var cameraRotationOffset = cameraInfo.Orientation;
            return (cameraRotationOffset - degrees + 360) % 360;
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

		public void SurfaceChanged(ISurfaceHolder holder, Android.Graphics.Format format, int width, int height)
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

                _camera.SetDisplayOrientation(GetRotationAngle());

				_camera.SetPreviewDisplay(holder);
				_camera.StartPreview();
			}
		}

	    private void SetCameraParameters(int width, int height)
	    {
            var parameters = _camera.GetParameters();

            if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            else if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousVideo))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousVideo;
            else
                parameters.FocusMode = Camera.Parameters.FocusModeAuto;

            var previewSize = GetOptimalPreviewSize(parameters.SupportedPreviewSizes, width, height);
            var pictureSize = GetOptimalPictureSize(previewSize, parameters.SupportedPictureSizes);

            parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
            parameters.SetPictureSize(pictureSize.Width, pictureSize.Height);

            _camera.SetParameters(parameters);
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
                _camera.SetPreviewCallback(this);
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

	    public void OnPreviewFrame(byte[] data, Camera camera)
	    {
            // TODO code for live stream is here
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
            const double ASPECT_TOLERANCE = 0.1;
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
    }
}