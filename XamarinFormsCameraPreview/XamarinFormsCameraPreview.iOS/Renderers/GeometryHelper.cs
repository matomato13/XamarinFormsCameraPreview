using System;
using System.Drawing;
using System.Linq;
using Emgu.CV;

namespace XamarinFormsCameraPreview.Helpers
{
    public static class GeometryHelper
    {
        public static PointF[] ToScaledPointFArray(Point[] contour, Size pictureSize, Size scaledDownPreviewSize)
        {
            // extrapolation of preview scaled down to fullsize picture
            var xScale = (float)pictureSize.Width / scaledDownPreviewSize.Width;
            var yScale = (float)pictureSize.Height / scaledDownPreviewSize.Height;

            // they should be ordered as topleft, topright, bottomright, bottomleft
            var orderedPoints = new PointF[4];

            var leftPoints = contour.OrderBy(x => x.X).Take(2).ToList();
            var rightPoints = contour.OrderByDescending(x => x.X).Take(2).ToList();

            orderedPoints[0] = leftPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).First();
            orderedPoints[1] = rightPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).First();
            orderedPoints[2] = rightPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).Last();
            orderedPoints[3] = leftPoints.OrderBy(x => x.Y).Select(x => new PointF(x.X * xScale, x.Y * yScale)).Last();

            return orderedPoints;
        }

        public static Image<TColor, byte> FourPointTransform<TColor>(Image<TColor, byte> grey, PointF[] orderedPoints)
            where TColor : struct, IColor
        {
            // obtain a consistent order of the points and unpack them individually
            var tl = orderedPoints[0];
            var tr = orderedPoints[1];
            var br = orderedPoints[2];
            var bl = orderedPoints[3];

            // compute the width of the new image, which will be the maximum distance between bottom-right and bottom-left
            // x-coordiates or the top-right and top-left x-coordinates
            var widthA = Math.Sqrt(Math.Pow(br.X - bl.X, 2) + Math.Pow(br.Y - bl.Y, 2));
            var widthB = Math.Sqrt(Math.Pow(tr.X - tl.X, 2) + Math.Pow(tr.Y - tl.Y, 2));
            var maxWidth = Math.Max(widthA, widthB);

            // compute the height of the new image, which will be the maximum distance between the top-right and bottom-right
            // y-coordinates or the top-left and bottom-left y-coordinates
            var heightA = Math.Sqrt(Math.Pow(tr.X - br.X, 2) + Math.Pow(tr.Y - br.Y, 2));
            var heightB = Math.Sqrt(Math.Pow(tl.X - bl.X, 2) + Math.Pow(tl.Y - bl.Y, 2));
            var maxHeight = Math.Max(heightA, heightB);

            // now that we have the dimensions of the new image, construct the set of destination points to obtain a "birds eye view",
            // (i.e. top-down view) of the image, again specifying points in the top-left, top-right, bottom-right, and bottom-left order
            var newSize = new Size((int)maxWidth, (int)maxHeight);
            var dst = new[]
            {
                new PointF(0, 0), new PointF(newSize.Width - 1, 0), new PointF(newSize.Width - 1, newSize.Height - 1), new PointF(0, newSize.Height - 1)
            };

            Image<TColor, byte> result;

            // compute the perspective transform matrix and then apply it
            using (var matrix = CvInvoke.GetPerspectiveTransform(orderedPoints, dst))
            {
                using (var warped = new Image<TColor, byte>(newSize))
                {
                    CvInvoke.WarpPerspective(grey, warped, matrix, newSize);
                    result = warped.Copy();
                }
            }

            return result;
        }
    }
}