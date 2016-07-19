using System;
using Android.Hardware;

namespace XamarinFormsCameraPreview.Droid.Renderers
{
	public class DelegatePictureCallback : Java.Lang.Object, Camera.IPictureCallback
	{
		public Action<byte[], Camera> PictureTaken { get; set; }

		public void OnPictureTaken(byte[] data, Camera camera)
		{
            PictureTaken?.Invoke(data, camera);
        }
	}
}