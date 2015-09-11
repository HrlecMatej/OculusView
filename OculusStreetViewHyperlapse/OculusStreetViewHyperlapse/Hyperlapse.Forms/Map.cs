
namespace Demo.WindowsPresentation
{
    using System.Windows.Controls;
    using System.Windows.Media;
    using GMap.NET.WindowsPresentation;
    using System.Globalization;
    using System.Windows;
    using System;
    using GMap.NET;

    /// <summary>
    /// the custom map f GMapControl 
    /// </summary>
    public class Map : GMapControl
    {
        public long ElapsedMilliseconds;

        DateTime start;
        DateTime end;
        int delta;

        public GDirections selectedDirection = null;
        public bool hasDirection = false;

        private int counter;
        readonly Typeface tf = new Typeface("GenericSansSerif");
        readonly System.Windows.FlowDirection fd = new System.Windows.FlowDirection();

        /// <summary>
        /// any custom drawing here
        /// </summary>
        /// <param name="drawingContext"></param>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (selectedDirection != null && hasDirection == true)
            {
                //start = DateTime.Now;



                //end = DateTime.Now;
                //delta = (int)(end - start).TotalMilliseconds;

                //FormattedText text = new FormattedText(string.Format(CultureInfo.InvariantCulture, "{0:0.0}", Zoom) + "z, " + MapProvider + ", refresh: " + counter++ + ", load: " + ElapsedMilliseconds + "ms, render: " + delta + "ms", CultureInfo.InvariantCulture, fd, tf, 20, Brushes.Blue);
                //drawingContext.DrawText(text, new Point(text.Height, text.Height));
                //text = null;


                SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF072527"));
                FormattedText text = new FormattedText("End location: " + selectedDirection.EndAddress + ".\nStart location: " + selectedDirection.StartAddress + ".\nDistance: " + selectedDirection.Distance + ", duration: " + selectedDirection.Duration, CultureInfo.InvariantCulture, fd, tf, 20, brush);

                SolidColorBrush boxy = new SolidColorBrush(Color.FromArgb(130, 180, 180, 180));
                drawingContext.DrawRectangle(boxy, new Pen(), new Rect(new Point(text.Height, text.Height), new Point(text.Height + text.Width, text.Height * 2)));


                drawingContext.DrawText(text, new Point(text.Height, text.Height));

                text = null;
            }
        }
    }
}
