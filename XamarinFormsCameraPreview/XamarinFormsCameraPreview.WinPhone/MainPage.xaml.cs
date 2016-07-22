using Microsoft.Phone.Controls;
using Xamarin.Forms;


namespace XamarinFormsCameraPreview.WinPhone
{
	public partial class MainPage : PhoneApplicationPage
	{
		public MainPage()
		{
			InitializeComponent();

			Forms.Init();
			Content = XamarinFormsCameraPreview.App.GetMainPage().ConvertPageToUIElement(this);
		}
	}
}
