using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using XamarinFormsCameraPreview.Views;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(XamarinFormsCameraPreview.iOS.Renderers.CameraPreviewRenderer))]

namespace XamarinFormsCameraPreview.iOS.Renderers
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, CameraPreviewView>
    {
		protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement == null)
            {
                var cameraPreviewView = new CameraPreviewView();

                var preview = e.NewElement;
                preview.PictureRequired += cameraPreviewView.OnPictureRequired;

                SetNativeControl(cameraPreviewView);
            }
        }
    }
}