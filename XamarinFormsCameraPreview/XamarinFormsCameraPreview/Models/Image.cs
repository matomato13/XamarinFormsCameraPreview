using Xamarin.Forms;

namespace XamarinFormsCameraPreview.Models
{
	public class Image
    {
        private ImageSource ImageSource { get; set; }

        public Image (ImageSource source)
        {
            ImageSource = source;
        }

        public ImageSource AsImageSource ()
        {
            return ImageSource;
        }
    }
}
