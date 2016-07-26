using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;
using XamarinFormsCameraPreview.Views;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(XamarinFormsCameraPreview.Droid.Renderers.CameraPreviewRenderer))]

namespace XamarinFormsCameraPreview.Droid.Renderers
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, CameraPreviewView>
    {
        protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
        {
            base.OnElementChanged(e);

            if (e.OldElement == null)
            {
                var cameraPreviewView = new CameraPreviewView(Context);

                var preview = e.NewElement;
                preview.PictureRequired += cameraPreviewView.OnPictureRequired;
                
                SetNativeControl(cameraPreviewView);
            }
        }
    }
}