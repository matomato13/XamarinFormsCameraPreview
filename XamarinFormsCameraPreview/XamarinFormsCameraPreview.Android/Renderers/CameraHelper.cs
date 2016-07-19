using System;
using System.Collections.Generic;
using Android.Content;
using Android.Hardware;
using Android.Runtime;
using Android.Views;

namespace XamarinFormsCameraPreview.Droid.Renderers
{
    public static class CameraHelper
    {
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

        public static int GetRotationAngle(Context context)
        {
            var degrees = 0;
            switch (context.GetSystemService(Context.WindowService).JavaCast<IWindowManager>().DefaultDisplay.Rotation)
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

        public static void SetCameraParameters(Context context, Camera camera, int width, int height)
        {
            var parameters = camera.GetParameters();

            // Set AutoFocus
            if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousPicture))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousPicture;
            else if (parameters.SupportedFocusModes.Contains(Camera.Parameters.FocusModeContinuousVideo))
                parameters.FocusMode = Camera.Parameters.FocusModeContinuousVideo;
            else
                parameters.FocusMode = Camera.Parameters.FocusModeAuto;

            var rotationAngle = GetRotationAngle(context);
            var isPortrait = rotationAngle == 90 || rotationAngle == 270;

            var previewSize = GetOptimalPreviewSize(parameters.SupportedPreviewSizes, isPortrait ? height : width, isPortrait ? width : height);

            parameters.SetPreviewSize(previewSize.Width, previewSize.Height);
            parameters.SetPictureSize(previewSize.Width, previewSize.Height);

            // Rotate preview and final picture
            camera.SetDisplayOrientation(rotationAngle);
            parameters.SetRotation(rotationAngle);

            camera.SetParameters(parameters);
        }

        public static Camera.Size GetOptimalPreviewSize(IList<Camera.Size> sizes, int w, int h)
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