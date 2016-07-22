using Xamarin.Forms;

namespace XamarinFormsCameraPreview
{
	public class App
	{
		public static Page GetMainPage()
		{
			return new NavigationPage(new Views.CapturePage());
		}
	}
}
