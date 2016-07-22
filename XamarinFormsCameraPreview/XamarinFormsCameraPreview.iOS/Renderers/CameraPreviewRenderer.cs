using System;
using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using XamarinFormsCameraPreview.Views;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(XamarinFormsCameraPreview.iOS.Renderers.CameraPreviewRenderer))]

namespace XamarinFormsCameraPreview.iOS.Renderers
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, CameraPreviewView>
    {
        private CameraPreviewView _cameraPreviewView;

		protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
        {
            base.OnElementChanged(e);

            if(e.OldElement == null)
            {
                // get CameraPreview object and set event handler
                var preview = e.NewElement;
                preview.PictureRequired += OnPictureRequired;

                _cameraPreviewView = new CameraPreviewView();

                SetNativeControl(_cameraPreviewView);
            }
        }

        private void OnPictureRequired (object sender, EventArgs e)
        {
            var preview = sender as CameraPreview;
            if(preview != null)
            {
                _cameraPreviewView.Toggle();
            }
        }

        protected override void OnElementPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            base.OnElementPropertyChanged(sender, e);

            switch (e.PropertyName)
            {
                case "Camera":
                    break;
                default:
                    break;
            }
        }
    }
}