using System;
using Xamarin.Forms;

namespace XamarinFormsCameraPreview.Views
{
	public partial class CapturePage
	{
		public CapturePage()
		{
			InitializeComponent();

            NavigationPage.SetHasNavigationBar(this, false);

            // The instance of camera preview uses platform-specified modules and it causes crash if it is defined in XAML.
            _cameraPreview.PictureTaken += preview__PictureTaken;
		}

		void preview__PictureTaken(object sender, PictureTakenEventArgs e)
		{
			Device.BeginInvokeOnMainThread(async () => {
				// create view image page
				var page = new ViewImagePage();
				var vm = new ViewModels.ViewImagePageViewModel {
					Image = e.Image.AsImageSource()
				};
				page.BindingContext = vm;

				// push view image page
				await Navigation.PushAsync(page);
			});
		}

		void OnCaptureButtonClicked(object sender, EventArgs e)
		{
			_cameraPreview.TakePicture();
		}
	}
}
