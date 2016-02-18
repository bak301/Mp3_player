using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace BMP3
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            init();
        }

        private TimeSpan totalDuration;
        private DispatcherTimer timerVideo;
        private List<StorageFile> lastOpened;

        private async void init() {
            lastOpened = new List<StorageFile>();
            StorageFile f =await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync("lastPlayList.txt", CreationCollisionOption.OpenIfExists);
            IList<string> listFile = await FileIO.ReadLinesAsync(f);

            foreach (string line in listFile) {
                lastOpened.Add(await StorageFile.GetFileFromPathAsync(line));
            }

            if (lastOpened.Count > 0) {
                currentPlaylist.ItemsSource = lastOpened;
            }

            currentPlaylist.DisplayMemberPath = "DisplayName";
        }

        private async void btnOpenFile_Click(object sender, RoutedEventArgs e) {
            Playlist tempPlaylist = await createNewPlaylist("temp");
            AddToCurrentPlaylist(tempPlaylist);
        }


        private async void btnConfirm_Click(object sender, RoutedEventArgs e) {
            Playlist newPlaylist = await createNewPlaylist(boxName.Text);
            StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync("playlist.json", CreationCollisionOption.OpenIfExists);

            string data = await FileIO.ReadTextAsync(file);
            List<Playlist> listPlaylistJSON = JsonConvert.DeserializeObject<List<Playlist>>(data);
            if (listPlaylistJSON == null) {
                listPlaylistJSON = new List<Playlist>();
            }

            listPlaylistJSON.Add(newPlaylist);
            string playlistJSON = JsonConvert.SerializeObject(listPlaylistJSON, Formatting.Indented);

            await FileIO.WriteTextAsync(file, playlistJSON);

            AddToCurrentPlaylist(newPlaylist);
        }

        private void btnCreatePlaylist_Click(object sender, RoutedEventArgs e) {
            getName.IsOpen = true;
        }

        private async Task<Playlist> createNewPlaylist(string name) {

            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".flac");
            picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;

            Playlist newPlaylist = new Playlist();
            newPlaylist.name = name;
            newPlaylist.list = (await picker.PickMultipleFilesAsync()).Select(el => el.Path).ToList();
            return newPlaylist;
        }

        private async void btnOpenPlaylist_Click(object sender, RoutedEventArgs e) {
            StorageFile f = await ApplicationData.Current.LocalFolder.GetFileAsync("playlist.json");
            string data = await FileIO.ReadTextAsync(f);
            List<Playlist> listPlaylist = JsonConvert.DeserializeObject<List<Playlist>>(data);

            listOfPlaylist.ItemsSource = listPlaylist;
            listOfPlaylist.DisplayMemberPath = "name";
        }

        private async void listOfPlaylist_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            Playlist selectedPlaylist = listOfPlaylist.SelectedItem as Playlist;

            List<StorageFile> listMedia = new List<StorageFile>();
            foreach (string path in selectedPlaylist.list) {
                StorageFile f = await StorageFile.GetFileFromPathAsync(path);
                listMedia.Add(f);
            }

            currentPlaylist.ItemsSource = listMedia; 
        }

        private async void AddToCurrentPlaylist(Playlist playlist) {
            List<StorageFile> tempListFile = new List<StorageFile>();
            foreach (string path in playlist.list) {
                tempListFile.Add(await StorageFile.GetFileFromPathAsync(path));
            }

            txtCurrentPlaylist.Text = playlist.name;
            currentPlaylist.ItemsSource = tempListFile;
            StorageFile firstFile = tempListFile.FirstOrDefault();

            if (playlist.list.Count > 0) {
                currentPlaylist.SelectedIndex = 0;
                playFile(firstFile);
            }
        }

        public async Task<IRandomAccessStream> ConvertToRandomAccessStream(MemoryStream memoryStream) {
            var randomAccessStream = new InMemoryRandomAccessStream();
            var outputStream = randomAccessStream.GetOutputStreamAt(0);
            var dw = new DataWriter(outputStream);
            var task = Task.Factory.StartNew(() => dw.WriteBytes(memoryStream.ToArray()));
            await task;
            await dw.StoreAsync();
            await outputStream.FlushAsync();
            return randomAccessStream;
        }

        private async void playFile(StorageFile f) {
            IRandomAccessStream stream ;
            try {
                 stream = await f.OpenAsync(FileAccessMode.Read);
            } catch (Exception) {
                stream = null;
                MessageDialog m = new MessageDialog("Some file can't be read ! try again later");
                await m.ShowAsync();
            }

            if (stream != null) {
                StorageItemThumbnail img = await f.GetThumbnailAsync(ThumbnailMode.SingleItem);
                Stream str = img.AsStreamForRead();
                BitmapImage thumbnail = new BitmapImage();

                MemoryStream ms = new MemoryStream();
                str.CopyTo(ms);
                IRandomAccessStream iras = await ConvertToRandomAccessStream(ms);
                await thumbnail.SetSourceAsync(iras);

                player.PosterSource = thumbnail;
                player.SetSource(stream, f.ContentType);
            }
            playCurrentMedia();
        }

        private void btnVol_Click(object sender, RoutedEventArgs e) {
            player.IsMuted = !player.IsMuted;
            btnVol.Icon = new SymbolIcon(player.IsMuted?Symbol.Mute:Symbol.Volume);

            if (player.IsMuted) {
                sliderVol.Value = 0;
            }
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e) {
            playCurrentMedia();
        }

        private void playCurrentMedia() {
            player.Play();
            btnPause.Visibility = Visibility.Visible;
            btnPlay.Visibility = Visibility.Collapsed;
        }

        private void btnPause_Click(object sender, RoutedEventArgs e) {
            player.Pause();

            btnPlay.Visibility = Visibility.Visible;
            btnPause.Visibility = Visibility.Collapsed;
        }

        private void btnStop_Click(object sender, RoutedEventArgs e) {
            player.Stop();

            btnPlay.Visibility = Visibility.Visible;
            btnPause.Visibility = Visibility.Collapsed;
        }

        private void btnPrev_Click(object sender, RoutedEventArgs e) {
            IReadOnlyList<StorageFile> temp = currentPlaylist.ItemsSource as IReadOnlyList<StorageFile>;
            if (currentPlaylist.SelectedIndex > 0 ) {
                currentPlaylist.SelectedIndex--;
            }
        }

        private void btnNext_Click(object sender, RoutedEventArgs e) {
            IReadOnlyList<StorageFile> temp = currentPlaylist.ItemsSource as IReadOnlyList<StorageFile>;
            if (currentPlaylist.SelectedIndex < temp.Count-1) {
                currentPlaylist.SelectedIndex++;
            }
        }

        private void sliderVol_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) {
            if (player != null) {
                player.Volume = sliderVol.Value / 100;
            }
        }

        private void player_MediaOpened(object sender, RoutedEventArgs e) {
            totalDuration = player.NaturalDuration.TimeSpan;
            timerVideo = new DispatcherTimer();
            timerVideo.Interval = TimeSpan.FromSeconds(1);
            timerVideo.Tick += timer_Tick;
            timerVideo.Start();

            progressDuration.Maximum = totalDuration.TotalSeconds;
            txtTotalDuration.Text = totalDuration.ToString("mm':'ss");
            txtPlaying.Text = (currentPlaylist.SelectedItem as StorageFile).Name;
        }

        private void timer_Tick(object sender, object e) {
            progressDuration.Value = (player.Position.TotalSeconds / totalDuration.TotalSeconds) * progressDuration.Maximum;
            txtCurrentPosition.Text = player.Position.ToString("mm':'ss");
        }

        private void currentPlaylist_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            player.Stop();
            playFile(currentPlaylist.SelectedItem as StorageFile);
        }

        private void progressDuration_PointerCaptureLost(object sender, PointerRoutedEventArgs e) {
            if (player != null) {
                double sec = (progressDuration.Value / progressDuration.Maximum) * player.NaturalDuration.TimeSpan.TotalSeconds;
                player.Position = TimeSpan.FromSeconds(sec);
            }
        }
    }
}
