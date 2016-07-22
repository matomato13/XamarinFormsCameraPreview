using System.ComponentModel;
using System.Runtime.CompilerServices;

using Xamarin.Forms;

namespace XamarinFormsCameraPreview.ViewModels
{
	public class ViewImagePageViewModel : INotifyPropertyChanged
	{
		ImageSource image_ = null;

		public ImageSource Image
		{
			get
			{
				return image_;
			}
			set
			{
				if (image_ != value) {
					image_ = value;
					NotifyPropertyChanged();
				}
			}
		}

        void NotifyPropertyChanged([CallerMemberName]string propertyName = null)
		{
			if (PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
	}
}
