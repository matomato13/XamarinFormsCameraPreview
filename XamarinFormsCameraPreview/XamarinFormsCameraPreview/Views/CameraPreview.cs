﻿using System;
using System.Windows.Input;
using Xamarin.Forms;

namespace XamarinFormsCameraPreview.Views
{
	/// <summary>
	/// A view which shows camera preview and provides function to take a picture
	/// </summary>
	public class CameraPreview : View
	{
		public static readonly BindableProperty PictureTakenCommandProperty =
			BindableProperty.Create<CameraPreview, ICommand>(x => x.PictureTakenCommand, null);

		/// <summary>
		/// Set/get a command object which is called when the picture is taken.
		/// <remarks>The taken image (Models.IImage) is set to a parameter.</remarks>
		/// </summary>
		public ICommand PictureTakenCommand
		{
			get
			{
				return (ICommand)GetValue(PictureTakenCommandProperty);
			}
			set
			{
				SetValue(PictureTakenCommandProperty, value);
			}
		}

		/// <summary>
		/// Take a picture
		/// </summary>
		/// <remarks>
		/// This method does not block the current thread
		/// </remarks>
		public void TakePicture()
		{
            PictureRequired?.Invoke(this, new EventArgs());
        }

		/// <summary>
		/// Notify that the picture is taken.
		/// In this function, PictureTaken event and PictureTakenCommand are called.
		/// </summary>
		/// <param name="image">taken picture, must not be null</param>
		public void OnPictureTaken(Models.IImage image)
		{
			if (image == null) { throw new ArgumentNullException("image"); }

            // call event
            PictureTaken?.Invoke(this, new PictureTakenEventArgs(image));

            // execute command
            if (PictureTakenCommand != null && PictureTakenCommand.CanExecute(image)) {
				PictureTakenCommand.Execute(image);
			}
		}

		/// <summary>
		/// The event which is called when the TakePicture() is called
		/// </summary>
		public event EventHandler PictureRequired;

		/// <summary>
		/// The event which is called when the picture is taken
		/// </summary>
		public event EventHandler<PictureTakenEventArgs> PictureTaken;
	}

	/// <summary>
	/// The event args class  of the PictureTaken event
	/// </summary>
	public sealed class PictureTakenEventArgs : EventArgs
	{
		public PictureTakenEventArgs(Models.IImage image)
		{
			Image = image;
		}

		public Models.IImage Image { get; private set; }
	}
}
