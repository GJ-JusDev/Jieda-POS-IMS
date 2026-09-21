using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Graphics.Imaging;
using ZXing;
using InventoryManagement.Application.Interfaces;
using InventoryManagement.UI.ViewModels.Base;
using Microsoft.Extensions.Logging;

namespace InventoryManagement.UI.ViewModels.Base;

public class CameraScannerViewModel : ViewModelBase, IAsyncDisposable
{
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private readonly BarcodeReaderGeneric _barcodeReader;
    private readonly DispatcherTimer _debounceTimer;
    private bool _isDebouncing;

    public Action? CloseAction { get; set; }
    public Action<string>? OnBarcodeDetected { get; set; }

    public ObservableCollection<MediaFrameSourceGroup> AvailableCameras { get; } = new();

    private MediaFrameSourceGroup? _selectedCamera;
    public MediaFrameSourceGroup? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (SetProperty(ref _selectedCamera, value))
            {
                _ = RestartCameraAsync();
            }
        }
    }

    private WriteableBitmap? _previewImage;
    public WriteableBitmap? PreviewImage
    {
        get => _previewImage;
        set => SetProperty(ref _previewImage, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isContinuousScan;
    public bool IsContinuousScan
    {
        get => _isContinuousScan;
        set => SetProperty(ref _isContinuousScan, value);
    }

    private bool _isCameraRunning;
    public bool IsCameraRunning
    {
        get => _isCameraRunning;
        private set => SetProperty(ref _isCameraRunning, value);
    }

    public ICommand CloseCommand { get; }

    public CameraScannerViewModel()
    {
        CloseCommand = new RelayCommand(_ => CloseAction?.Invoke());

        _barcodeReader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new[] { BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.QR_CODE, BarcodeFormat.UPC_A }
            }
        };

        _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _debounceTimer.Tick += (s, e) =>
        {
            _isDebouncing = false;
            _debounceTimer.Stop();
            StatusMessage = "Scanning...";
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "Looking for cameras...";
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            var colorCameras = groups.Where(g => g.SourceInfos.Any(i => i.SourceKind == MediaFrameSourceKind.Color)).ToList();

            foreach (var cam in colorCameras)
            {
                AvailableCameras.Add(cam);
            }

            if (AvailableCameras.Any())
            {
                SelectedCamera = AvailableCameras.First();
            }
            else
            {
                StatusMessage = "No camera detected.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Camera error: {ex.Message}";
        }
    }

    private async Task RestartCameraAsync()
    {
        await StopCameraAsync();

        if (SelectedCamera == null) return;

        try
        {
            StatusMessage = "Camera starting...";
            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = SelectedCamera,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            });

            var colorSource = _mediaCapture.FrameSources.Values.FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color);
            if (colorSource == null)
            {
                StatusMessage = "Camera does not support color output.";
                return;
            }

            _frameReader = await _mediaCapture.CreateFrameReaderAsync(colorSource, Windows.Media.MediaProperties.MediaEncodingSubtypes.Bgra8);
            _frameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _frameReader.FrameArrived += FrameReader_FrameArrived;
            await _frameReader.StartAsync();

            IsCameraRunning = true;
            StatusMessage = "Scanning...";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Permission to access the camera was denied.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Camera unavailable: {ex.Message}";
        }
    }

    private async Task StopCameraAsync()
    {
        IsCameraRunning = false;
        if (_frameReader != null)
        {
            _frameReader.FrameArrived -= FrameReader_FrameArrived;
            await _frameReader.StopAsync();
            _frameReader.Dispose();
            _frameReader = null;
        }

        if (_mediaCapture != null)
        {
            _mediaCapture.Dispose();
            _mediaCapture = null;
        }
    }

    private void FrameReader_FrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (_isDebouncing) return;

        using var frame = sender.TryAcquireLatestFrame();
        var softwareBitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
        if (softwareBitmap == null) return;

        // Decode barcode
        var width = softwareBitmap.PixelWidth;
        var height = softwareBitmap.PixelHeight;
        var bytes = new byte[4 * width * height];
        softwareBitmap.CopyToBuffer(bytes.AsBuffer());

        var luminanceSource = new RGBLuminanceSource(bytes, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var result = _barcodeReader.Decode(luminanceSource);

        if (result != null)
        {
            _isDebouncing = true; // Set immediately on background thread
        }

        // Update UI Preview
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (PreviewImage == null || PreviewImage.PixelWidth != width || PreviewImage.PixelHeight != height)
            {
                PreviewImage = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
            }
            PreviewImage.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), bytes, width * 4, 0);

            if (result != null)
            {
                StatusMessage = $"Barcode detected: {result.Text}";
                
                System.Media.SystemSounds.Beep.Play(); // Optional success beep
                
                OnBarcodeDetected?.Invoke(result.Text);

                if (IsContinuousScan)
                {
                    _debounceTimer.Start();
                }
                else
                {
                    // Close the window after slight delay for visual feedback
                    Task.Delay(500).ContinueWith(_ => 
                        System.Windows.Application.Current.Dispatcher.Invoke(() => CloseAction?.Invoke())
                    );
                }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        _debounceTimer.Stop();
        await StopCameraAsync();
    }
}
