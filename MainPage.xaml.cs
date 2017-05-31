using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;


namespace VisualiseTasks
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            var data = GetData();
            DrawGraph(data);
        }

        private List<DataReading> GetData()
        {
            var results = new List<DataReading>();
            var rnd = new Random();

            var slots = new DateTime[10];
            for (int i = 0; i < 10; i++)
            {
                slots[i] = new DateTime(2017, 1, 1);
            }

            for (int table = 0; table < 50; table++)
            {
                for (int buyer = 0; buyer < 40; buyer++)
                {
                    //Find the first free slot
                    (int slot, DateTime endTime) earliestSlot = (0, DateTime.MaxValue);
                    for (int i = 0; i < 10; i++)
                    {
                        if (slots[i] < earliestSlot.endTime)
                        {
                            earliestSlot.endTime = slots[i];
                            earliestSlot.slot = i;
                        }
                    }

                    var endTime = earliestSlot.endTime.AddSeconds(rnd.Next(0, 120));
                    results.Add(new DataReading()
                    {
                        BuyerName = $"Buyer{buyer}",
                        NumberOfRows = rnd.Next(0, 10000),
                        StartTime = earliestSlot.endTime,
                        EndTime = endTime,
                        TableName = $"Table{table}"
                    });

                    slots[earliestSlot.slot] = endTime;
                }
            }

            return results;
        }

        private void DrawGraph(List<DataReading> data)
        {
            const int TOTAL_NUMBER_OF_SLOTS = 10;
            const int SLOT_WIDTH = 20;
            const double TOTAL_HEIGHT = 150.0;
            const int Y_AXIS = 25;

            // Order the data by start time,  as we want to draw them in time order
            var orderedData = data.OrderBy(d => d.StartTime);

            // Work out how much space time takes
            var firstTime = orderedData.First().StartTime;
            var finalTime = orderedData.Max(a => a.EndTime);
            var totalSeconds = (finalTime - firstTime).TotalSeconds;
            var heightPerSecond = TOTAL_HEIGHT / totalSeconds;

            // For each slot we need to track where the next available time gap is
            var slotEndTimes = new DateTime[TOTAL_NUMBER_OF_SLOTS];

            // Work out the maximum number of rows so that we can work out a sensible colour range.
            // We use 0 as our lower bound as it's quite likely we will have a small/empty table.
            var largestRowCount = orderedData.Max(a => a.NumberOfRows);

            var black = new SolidColorBrush(Windows.UI.Colors.Black);
            foreach (var dataPoint in orderedData)
            {
                // Find the next available slot,  ie the first one whose endDate is before now
                var slot = Array.FindIndex(slotEndTimes, 0, a => a <= dataPoint.StartTime);
                var x = Y_AXIS + (slot * SLOT_WIDTH);
                slotEndTimes[slot] = dataPoint.EndTime;

                // Start of the frame is calculated from the time
                var secondsSinceStart = (dataPoint.StartTime - firstTime).TotalSeconds;
                var y = secondsSinceStart * heightPerSecond;

                // Height is based on the number of seconds it took to run
                var numberOfSeconds = (dataPoint.EndTime - dataPoint.StartTime).TotalSeconds;
                var height = numberOfSeconds * heightPerSecond;

                // Colour is based on the number of rows
                var colour = CalculateColour(largestRowCount, dataPoint.NumberOfRows);

                var rect = new Rectangle() { Width = SLOT_WIDTH, Height = height, Fill = colour, Stroke = black, StrokeThickness = .1, Tag = dataPoint };
                rect.PointerEntered += Rect_PointerEntered;
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                canvas.Children.Add(rect);
            }

            //Y axis
            const int NUMBER_OF_TIME_SLICES = 10;
            for (int timeSlice = 0; timeSlice < NUMBER_OF_TIME_SLICES; timeSlice++)
            {
                var offsetInSeconds = (totalSeconds / NUMBER_OF_TIME_SLICES) * timeSlice;
                var actualTime = firstTime.AddSeconds(offsetInSeconds);
                var asTime = actualTime.ToString("HH:mm:ss");

                var tb = new TextBlock()
                {
                    Text = $"{asTime}",
                    FontSize = 6,
                    VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top
                };

                var y = -1 + (offsetInSeconds * heightPerSecond);
                Canvas.SetLeft(tb, 2);
                Canvas.SetTop(tb, y);

                var line = new Line()
                {
                    X1 = 25,
                    X2 = 20,
                    Y1 = y,
                    Y2 = y,
                    Stroke = black,
                    StrokeThickness = .2
                };

                this.canvas.Children.Add(tb);
                this.canvas.Children.Add(line);

            }


            // X axis
            for (int slotNumber = 0; slotNumber < TOTAL_NUMBER_OF_SLOTS; slotNumber++)
            {
                var tb = new TextBlock()
                {
                    Text = $"{slotNumber}",
                    FontSize = 6
                };

                Canvas.SetLeft(tb, Y_AXIS + 5 + (slotNumber * SLOT_WIDTH));
                Canvas.SetTop(tb, TOTAL_HEIGHT);

                this.canvas.Children.Add(tb);
            }

            //Rows key
            this.rows1.Text = "1 Row";
            this.rows1.Foreground = CalculateColour(largestRowCount, 1);

            this.rows2.Text = $"{largestRowCount / 4 } Rows";
            this.rows2.Foreground = CalculateColour(largestRowCount, largestRowCount / 4);

            this.rows3.Text = $"{largestRowCount / 2} Rows";
            this.rows3.Foreground = CalculateColour(largestRowCount, largestRowCount / 2);

            this.rows4.Text = $"{largestRowCount / 4 * 3} Rows";
            this.rows4.Foreground = CalculateColour(largestRowCount, largestRowCount / 4 * 3);

            this.rows5.Text = $"{largestRowCount} Rows";
            this.rows5.Foreground = CalculateColour(largestRowCount, largestRowCount);

        }
        
        private static SolidColorBrush CalculateColour(int largestRowCount, int numberOfRows)
        {
            var hue = Math.Floor((largestRowCount - numberOfRows) * 120.0 / largestRowCount);
            var colour = new SolidColorBrush(ColorHelper.FromHsv(hue, 1, 1));
            return colour;
        }

        private void Rect_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Rectangle rect)
            {
                if (rect.Tag is DataReading dataPoint)
                {
                    this.datapointBuyerName.Text = dataPoint.BuyerName;
                    this.datapointTable.Text = dataPoint.TableName;
                    this.datapointStart.Text = dataPoint.StartTime.ToString("hh:mm:ss");
                    this.datapointEnd.Text = dataPoint.EndTime.ToString("hh:mm:ss");
                    this.datapointSeconds.Text = $"{(dataPoint.EndTime - dataPoint.StartTime).TotalSeconds}s";
                    this.datapointRows.Text = dataPoint.NumberOfRows.ToString();
                }
            }
        }


    }
}
