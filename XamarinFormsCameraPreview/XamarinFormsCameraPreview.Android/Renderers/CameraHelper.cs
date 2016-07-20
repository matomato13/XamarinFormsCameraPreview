using System;
using System.Collections.Generic;
using System.Drawing;
using Android.Graphics;
using Android.Hardware;
using Android.Views;
using ApxLabs.FastAndroidCamera;
using Camera = Android.Hardware.Camera;

namespace XamarinFormsCameraPreview.Droid.Renderers
{
    public static class CameraHelper
    {
        public static int GetRotationAngle(SurfaceOrientation orientation, Camera.CameraInfo cameraInfo)
        {
            var degrees = 0;
            switch (orientation)
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

            return (cameraInfo.Orientation - degrees + 360) % 360;
        }

        public static Size SetCameraParameters(SurfaceOrientation deviceOrientation, Camera.CameraInfo cameraInfo, Camera camera, int width, int height, IList<FastJavaByteArray> buffers, Size previewSize)
        {
            var parameters = camera.GetParameters();

            // Set AutoFocus
            if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            else if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousVideo))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousVideo;
            else
                parameters.FocusMode = Camera.Parameters.FocusModeAuto;

            var rotationAngle = GetRotationAngle(deviceOrientation, cameraInfo);
            var isPortrait = rotationAngle == 90 || rotationAngle == 270;

            if (previewSize.Width == 0 || previewSize.Height == 0)
            {
                var optimalPreviewSize = GetOptimalPreviewSize(parameters.SupportedPreviewSizes, isPortrait ? height : width, isPortrait ? width : height);

                parameters.SetPreviewSize(optimalPreviewSize.Width, optimalPreviewSize.Height);
                parameters.SetPictureSize(optimalPreviewSize.Width, optimalPreviewSize.Height);

                previewSize = new Size(optimalPreviewSize.Width, optimalPreviewSize.Height);
            }
            
            // Rotate preview and final picture
            camera.SetDisplayOrientation(rotationAngle);
            parameters.SetRotation(rotationAngle);

            if (buffers.Count == 0)
            {
                var buffersize = CalculateBufferSize(parameters);

                for (var i = 0; i <= 3; i++)
                {
                    buffers.Add(new FastJavaByteArray(buffersize));
                }
            }

            foreach (var buffer in buffers)
            {
                camera.AddCallbackBuffer(buffer);
            }

            camera.SetParameters(parameters);

            return previewSize;
        }

        private static int CalculateBufferSize(Camera.Parameters parameters)
        {
            return parameters.PreviewSize.Width * parameters.PreviewSize.Height * ImageFormat.GetBitsPerPixel(parameters.PreviewFormat) / 8;
        }

        public static Camera.CameraInfo GetCameraInfo()
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

        private static Camera.Size GetOptimalPreviewSize(IList<Camera.Size> sizes, int w, int h)
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
    }
}