using System;
using AVFoundation;
using CoreGraphics;
using CoreMedia;
using CoreVideo;
using UIKit;

namespace XamarinFormsCameraPreview.iOS.Renderers
{
    public class OutputRecorder : AVCaptureVideoDataOutputSampleBufferDelegate
    {
        private readonly Func<UIImage, UIImage> _processImage;

        public OutputRecorder (Func<UIImage, UIImage> processImage)
        {
            _processImage = processImage;
        }

        public override void DidOutputSampleBuffer (AVCaptureOutput captureOutput, CMSampleBuffer sampleBuffer, AVCaptureConnection connection)
        {
            try
            {
                var image = ImageFromSampleBuffer(sampleBuffer);

                var contourImage = _processImage(image);
                if(contourImage == null)
                {
                    return;
                }

                // Do something with the image, we just stuff it in our main view.
                CameraPreviewView.ImageView.BeginInvokeOnMainThread(() => {
                    TryDispose(CameraPreviewView.ImageView.Image);
                    CameraPreviewView.ImageView.Image = contourImage;
                });
            }
            catch(Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                sampleBuffer.Dispose();
            }
        }

        private UIImage ImageFromSampleBuffer (CMSampleBuffer sampleBuffer)
        {
            // Get the CoreVideo image
            using(var pixelBuffer = sampleBuffer.GetImageBuffer() as CVPixelBuffer)
            {
                // Lock the base address
                pixelBuffer.Lock(CVPixelBufferLock.None);

                // Get the number of bytes per row for the pixel buffer
                var baseAddress = pixelBuffer.BaseAddress;
                var bytesPerRow = (int)pixelBuffer.BytesPerRow;
                var width = (int)pixelBuffer.Width;
                var height = (int)pixelBuffer.Height;
                var flags = CGBitmapFlags.PremultipliedFirst | CGBitmapFlags.ByteOrder32Little;

                // Create a CGImage on the RGB colorspace from the configured parameter above
                using(var cs = CGColorSpace.CreateDeviceRGB())
                {
                    using(var context = new CGBitmapContext(baseAddress, width, height, 8, bytesPerRow, cs, (CGImageAlphaInfo)flags))
                    {
                        using(CGImage cgImage = context.ToImage())
                        {
                            pixelBuffer.Unlock(CVPixelBufferLock.None);
                            return UIImage.FromImage(cgImage);
                        }
                    }
                }
            }
        }

        private void TryDispose (IDisposable obj)
        {
            obj?.Dispose();
        }
    }
}

