using AVFoundation;
using CoreGraphics;
using Foundation;
using UIKit;

namespace XamarinFormsCameraPreview.iOS.Renderers
{
    /// <summary>
    /// Class CameraPreviewView.
    /// </summary>
    [Register("CameraPreviewView")]
    public class CameraPreviewView : UIView
    {
        /// <summary>
        /// The _preview layer
        /// </summary>
        private AVCaptureVideoPreviewLayer _previewLayer;

        /// <summary>
        /// Initializes a new instance of the <see cref="CameraPreviewView"/> class.
        /// </summary>
        public CameraPreviewView()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CameraPreviewView"/> class.
        /// </summary>
        /// <param name="bounds">The bounds.</param>
        public CameraPreviewView(CGRect bounds)
            : base(bounds)
        {
            Initialize();
        }

        /// <summary>
        /// Draws the specified rect.
        /// </summary>
        /// <param name="rect">The rect.</param>
        public override void Draw(CGRect rect)
        {
            base.Draw(rect);
            _previewLayer.Frame = rect;
        }

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        private void Initialize()
        {
            var captureSession = new AVCaptureSession();
            _previewLayer = new AVCaptureVideoPreviewLayer(captureSession)
            {
                VideoGravity = AVLayerVideoGravity.ResizeAspectFill,
                Frame = Bounds
            };

            var device = AVCaptureDevice.DefaultDeviceWithMediaType(AVMediaType.Video);

            if (device == null)
            {
                System.Diagnostics.Debug.WriteLine("No device detected.");
                return;
            }

            NSError error;

            var input = new AVCaptureDeviceInput(device, out error);

            captureSession.AddInput(input);

            Layer.AddSublayer(_previewLayer);

            captureSession.StartRunning();
        }
    }
}