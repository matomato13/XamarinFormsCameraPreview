using Xamarin.Forms;
using Xamarin.Forms.Platform.iOS;
using XamarinFormsCameraPreview.Views;

[assembly: ExportRenderer(typeof(CameraPreview), typeof(XamarinFormsCameraPreview.iOS.Renderers.CameraPreviewRenderer))]

namespace XamarinFormsCameraPreview.iOS.Renderers
{
    public class CameraPreviewRenderer : ViewRenderer<CameraPreview, CameraPreviewView>
    {
        /// <summary>
		/// Called when [element changed].
		/// </summary>
		/// <param name="e">The e.</param>
		protected override void OnElementChanged(ElementChangedEventArgs<CameraPreview> e)
        {
            base.OnElementChanged(e);

            if (Control == null)
            {
                SetNativeControl(new CameraPreviewView());
            }
        }

        /// <summary>
        /// Handles the <see cref="E:ElementPropertyChanged" /> event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.ComponentModel.PropertyChangedEventArgs"/> instance containing the event data.</param>
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