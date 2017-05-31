using Microsoft.Toolkit.Uwp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml;
using Windows.Storage;
using System.Text;
using Windows.Storage.Streams;

namespace VisualiseTasks
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// The data format used in the CSV file
        /// </summary>
        private const string DATE_FORMAT = "yyyy-MM-dd hh:mm:ss";
        public MainPage()
        {
            this.InitializeComponent();

        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var data = await GetDataFromAFile();
            //var data = GetTestData();
            //await SaveData(data);
            DrawGraph(data);
        }

        private async Task SaveData(List<DataReading> data)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            savePicker.FileTypeChoices.Add("CSV File ", new List<string>() { ".csv" });
            savePicker.SuggestedFileName = "Data";
            var file = await savePicker.PickSaveFileAsync();
            if (file == null) Application.Current.Exit();

            var resultsCSV = new Chilkat.Csv();
            resultsCSV.HasColumnNames = true;
            resultsCSV.SetColumnName(0, "Start");
            resultsCSV.SetColumnName(1, "End");
            resultsCSV.SetColumnName(2, "Buyer");
            resultsCSV.SetColumnName(3, "Table");
            resultsCSV.SetColumnName(4, "Rows");

            var rowNumber = 0;
            foreach (var dataPoint in data)
            {
                resultsCSV.SetCell(rowNumber, 0, dataPoint.StartTime.ToString(DATE_FORMAT));
                resultsCSV.SetCell(rowNumber, 1, dataPoint.EndTime.ToString(DATE_FORMAT));
                resultsCSV.SetCell(rowNumber, 2, dataPoint.BuyerName);
                resultsCSV.SetCell(rowNumber, 3, dataPoint.TableName);
                resultsCSV.SetCell(rowNumber, 4, dataPoint.NumberOfRows.ToString());
                rowNumber++;
            }

            var csvDoc = resultsCSV.SaveToString();
            await Windows.Storage.FileIO.WriteTextAsync(file, csvDoc);
        }

        private async Task<List<DataReading>> GetDataFromAFile()
        {
            DateTime ParseDate(string value, int lineNumber)
            {
                if (DateTime.TryParseExact(value, DATE_FORMAT, null, System.Globalization.DateTimeStyles.None, out var result))
                    return result;

                throw new Exception($"The date on line {lineNumber} must be in the format {DATE_FORMAT},  not {value}");
            }

            int ParseNumber(string value, int lineNumber)
            {
                if (int.TryParse(value, out var result))
                    return result;

                throw new Exception($"The date on line {lineNumber} must be numeric, not {value}");
            }


            var file = await AskForFile();
            if (file == null) Application.Current.Exit();

            // Can't use this due to some strange encoding issue
            // var contents = await Windows.Storage.FileIO.ReadTextAsync(file);
            var buffer = await FileIO.ReadBufferAsync(file);
            var reader = DataReader.FromBuffer(buffer);
            var fileContent = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(fileContent);
            var contents = Encoding.UTF8.GetString(fileContent, 0, fileContent.Length);

            var csv = new Chilkat.Csv
            {
                //  Prior to loading the CSV file, indicate that the 1st row should be treated as column names:
                HasColumnNames = true
            };

            var success = csv.LoadFromString(contents);
            if (success != true)
            {
                throw new Exception("Failed to read the index csv file " + csv.LastErrorText);
            }

            var data = new List<DataReading>(csv.NumRows);
            for (var rowNumber = 0; rowNumber <= csv.NumRows - 1; rowNumber++)
            {
                data.Add(new DataReading()
                {
                    StartTime = ParseDate(csv.GetCell(rowNumber, 0), rowNumber),
                    EndTime = ParseDate(csv.GetCell(rowNumber, 1), rowNumber),
                    BuyerName = csv.GetCell(rowNumber, 2),
                    TableName = csv.GetCell(rowNumber, 3),
                    NumberOfRows = ParseNumber(csv.GetCell(rowNumber, 4), rowNumber),
                });
            }

            return data;
        }

        private List<DataReading> GetTestData()
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

                    if (buyer==2 && table == 10)
                        endTime = earliestSlot.endTime.AddSeconds(60*10);

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

        private async Task<Windows.Storage.StorageFile> AskForFile()
        {
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            openPicker.FileTypeFilter.Add(".csv");
            var file = await openPicker.PickSingleFileAsync();

            if (file != null)
            {
                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(file);
            }

            return file;
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
