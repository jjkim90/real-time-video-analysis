using OpenCvSharp; // OpenCVSharp 네임스페이스
using OpenCvSharp.WpfExtensions; // BitmapSourceConverter 사용
using Prism.Commands;
using Prism.Mvvm;
using System.Windows.Media.Imaging; // ImageSource, BitmapSource
using Microsoft.Win32; // OpenFileDialog
using System; // IDisposable
using System.Threading;
using System.Threading.Tasks; // Task
using System.Windows; // MessageBox
using RealTimeVideoAnalysis.Models; // RoiModel 및 ImageEffectType 사용을 위해 추가
using System.Windows.Input; // MouseButtonEventArgs, MouseEventArgs 등 사용 위해 (향후 커맨드에서 필요시)
using RealTimeVideoAnalysis.Services; // SettingsService 사용을 위해 추가
using System.IO; // Path, Directory
using System.Diagnostics; // Stopwatch
using System.Linq; // Contains for array

namespace RealTimeVideoAnalysis.ViewModels
{
    public class MainWindowViewModel : BindableBase, IDisposable
    {
        #region --- 필드 ---

        private VideoCapture _capture;
        private Mat _frame;
        private bool _isPlaying = false;
        private bool _isPaused = false;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _videoProcessingTask;
        private readonly object _roiLock = new object();
        private readonly MatPool _matPool = new MatPool();
        
        // IDisposable 패턴 지원
        private bool _disposed = false;
        private readonly object _disposeLock = new object();

        private System.Windows.Point _roiStartPoint;
        private bool _isRoiDrawing = false;
        
        // 프레임 탐색
        private int _currentFramePosition = 0;
        private int _totalFrameCount = 0;
        private bool _isVideoFile = false;
        private bool _isSliderBeingDragged = false;
        
        // 성능 최적화
        private (double width, double height, double offsetX, double offsetY)? _cachedImageDimensions;
        private int _lastFrameCols = 0;
        private int _lastFrameRows = 0;
        private double _lastImageRenderWidth = 0;
        private double _lastImageRenderHeight = 0;
        
        // 설정 서비스
        private readonly ISettingsService _settingsService;
        
        // 비디오 녹화
        private VideoWriter _videoWriter;
        private bool _isRecording = false;
        private string _recordingFilePath;
        
        // 성능 모니터링
        private Stopwatch _frameStopwatch = new Stopwatch();
        private Stopwatch _fpsStopwatch = new Stopwatch();
        private int _frameCount = 0;
        private double _currentFps = 0;
        private double _currentProcessingTime = 0;

        #endregion

        #region --- UI 바인딩 속성들 ---

        private string _title = "실시간 영상 분석 (Prism + OpenCVSharp)";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private BitmapSource _videoFrame;
        public BitmapSource VideoFrame
        {
            get { return _videoFrame; }
            set { SetProperty(ref _videoFrame, value); }
        }

        private string _playPauseButtonText = "재생";
        public string PlayPauseButtonText
        {
            get { return _playPauseButtonText; }
            set { SetProperty(ref _playPauseButtonText, value); }
        }
        
        private string _playPauseIcon = "";
        public string PlayPauseIcon
        {
            get { return _playPauseIcon; }
            set { SetProperty(ref _playPauseIcon, value); }
        }

        private string _statusText = "준비됨";
        public string StatusText
        {
            get { return _statusText; }
            set { SetProperty(ref _statusText, value); }
        }

        private RoiModel _currentRoi;
        public RoiModel CurrentRoi
        {
            get { return _currentRoi; }
            set { SetProperty(ref _currentRoi, value); }
        }

        private double _imageRenderWidth;
        public double ImageRenderWidth
        {
            get { return _imageRenderWidth; }
            set { SetProperty(ref _imageRenderWidth, value); }
        }

        private double _imageRenderHeight;
        public double ImageRenderHeight
        {
            get { return _imageRenderHeight; }
            set { SetProperty(ref _imageRenderHeight, value); }
        }

        private ImageEffectType _currentImageEffect = ImageEffectType.None;
        public ImageEffectType CurrentImageEffect
        {
            get { return _currentImageEffect; }
            set { SetProperty(ref _currentImageEffect, value); }
        }
        
        // --- 조정 가능한 매개변수들 ---
        private int _targetFps = 30;
        public int TargetFps
        {
            get { return _targetFps; }
            set
            {
                if (SetProperty(ref _targetFps, Math.Max(1, Math.Min(60, value))))
                {
                    _frameDelay = 1000 / _targetFps;
                }
            }
        }
        private int _frameDelay = 33;

        private double _binaryThreshold = 128;
        public double BinaryThreshold
        {
            get { return _binaryThreshold; }
            set { SetProperty(ref _binaryThreshold, Math.Max(0, Math.Min(255, value))); }
        }

        private double _blurStrength = 15;
        public double BlurStrength
        {
            get { return _blurStrength; }
            set { SetProperty(ref _blurStrength, Math.Max(3, Math.Min(31, value))); }
        }

        private double _sharpenStrength = 3.0;
        public double SharpenStrength
        {
            get { return _sharpenStrength; }
            set { SetProperty(ref _sharpenStrength, Math.Max(0, Math.Min(5, value))); }
        }

        private double _brightness = 0;
        public double Brightness
        {
            get { return _brightness; }
            set { SetProperty(ref _brightness, Math.Max(-100, Math.Min(100, value))); }
        }

        private double _contrast = 1.0;
        public double Contrast
        {
            get { return _contrast; }
            set { SetProperty(ref _contrast, Math.Max(0.0, Math.Min(2.0, value))); }
        }
        
        // HSV 색상 검출 범위 속성들
        private double _hueLower = 0;
        public double HueLower
        {
            get { return _hueLower; }
            set { SetProperty(ref _hueLower, Math.Max(0, Math.Min(179, value))); }
        }

        private double _hueUpper = 179;
        public double HueUpper
        {
            get { return _hueUpper; }
            set { SetProperty(ref _hueUpper, Math.Max(0, Math.Min(179, value))); }
        }

        private double _saturationLower = 50;
        public double SaturationLower
        {
            get { return _saturationLower; }
            set { SetProperty(ref _saturationLower, Math.Max(0, Math.Min(255, value))); }
        }

        private double _saturationUpper = 255;
        public double SaturationUpper
        {
            get { return _saturationUpper; }
            set { SetProperty(ref _saturationUpper, Math.Max(0, Math.Min(255, value))); }
        }

        private double _valueLower = 50;
        public double ValueLower
        {
            get { return _valueLower; }
            set { SetProperty(ref _valueLower, Math.Max(0, Math.Min(255, value))); }
        }

        private double _valueUpper = 255;
        public double ValueUpper
        {
            get { return _valueUpper; }
            set { SetProperty(ref _valueUpper, Math.Max(0, Math.Min(255, value))); }
        }
        
        // 프레임 탐색 속성들
        public int CurrentFramePosition
        {
            get { return _currentFramePosition; }
            set 
            { 
                if (SetProperty(ref _currentFramePosition, value))
                {
                    // 슬라이더 값 변경 시 프레임 탐색
                    if (_isVideoFile && _isPaused && _isSliderBeingDragged)
                    {
                        SeekToFrame(value);
                    }
                }
            }
        }
        
        public int TotalFrameCount
        {
            get { return _totalFrameCount; }
            set { SetProperty(ref _totalFrameCount, value); }
        }
        
        private string _frameInfo = "";
        public string FrameInfo
        {
            get { return _frameInfo; }
            set { SetProperty(ref _frameInfo, value); }
        }
        
        public bool CanExecuteFrameNavigation => _capture != null && _isVideoFile && _isPaused;
        
        private bool _isRecordingActive = false;
        public bool IsRecordingActive
        {
            get { return _isRecordingActive; }
            set { SetProperty(ref _isRecordingActive, value); }
        }
        
        private string _recordingStatus = "";
        public string RecordingStatus
        {
            get { return _recordingStatus; }
            set { SetProperty(ref _recordingStatus, value); }
        }
        
        public double CurrentFps
        {
            get { return _currentFps; }
            set { SetProperty(ref _currentFps, value); }
        }
        
        public double CurrentProcessingTime
        {
            get { return _currentProcessingTime; }
            set { SetProperty(ref _currentProcessingTime, value); }
        }
        
        private string _performanceInfo = "";
        public string PerformanceInfo
        {
            get { return _performanceInfo; }
            set { SetProperty(ref _performanceInfo, value); }
        }
        
        private bool _hasError = false;
        public bool HasError
        {
            get { return _hasError; }
            set { SetProperty(ref _hasError, value); }
        }
        
        private string _errorMessage = "";
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }
        #endregion

        #region --- 커맨드 ---
        public DelegateCommand StartWebcamCommand { get; private set; }
        public DelegateCommand OpenFileCommand { get; private set; }
        public DelegateCommand PlayPauseCommand { get; private set; }
        public DelegateCommand StopCommand { get; private set; }
        public DelegateCommand<object> RoiMouseDownCommand { get; private set; }
        public DelegateCommand<object> RoiMouseMoveCommand { get; private set; }
        public DelegateCommand<object> RoiMouseUpCommand { get; private set; }
        public DelegateCommand ClearRoiCommand { get; private set; }
        public DelegateCommand ApplyBinaryEffectCommand { get; private set; }
        public DelegateCommand ApplyGrayscaleEffectCommand { get; private set; }
        public DelegateCommand ApplyGaussianBlurEffectCommand { get; private set; }
        public DelegateCommand ApplySharpenEffectCommand { get; private set; }
        public DelegateCommand ApplyColorDetectionCommand { get; private set; }
        public DelegateCommand ClearImageEffectCommand { get; private set; }
        public DelegateCommand ResetBrightnessContrastCommand { get; private set; }
        public DelegateCommand SaveSettingsCommand { get; private set; }
        public DelegateCommand LoadSettingsCommand { get; private set; }
        public DelegateCommand PreviousFrameCommand { get; private set; }
        public DelegateCommand NextFrameCommand { get; private set; }
        public DelegateCommand<object> SliderDragStartedCommand { get; private set; }
        public DelegateCommand<object> SliderDragCompletedCommand { get; private set; }
        public DelegateCommand CaptureScreenshotCommand { get; private set; }
        public DelegateCommand ToggleRecordingCommand { get; private set; }
        public DelegateCommand ClearErrorCommand { get; private set; }
        #endregion

        #region --- 생성자 ---
        public MainWindowViewModel()
        {
            _settingsService = new SettingsService();
            StartWebcamCommand = new DelegateCommand(ExecuteStartWebcam, CanExecuteStartWebcam);
            OpenFileCommand = new DelegateCommand(ExecuteOpenFile, CanExecuteOpenFile);
            PlayPauseCommand = new DelegateCommand(ExecutePlayPause, CanExecutePlayPause);
            StopCommand = new DelegateCommand(ExecuteStop, CanExecuteStop);

            CurrentRoi = new RoiModel();

            RoiMouseDownCommand = new DelegateCommand<object>(ExecuteRoiMouseDown);
            RoiMouseMoveCommand = new DelegateCommand<object>(ExecuteRoiMouseMove);
            RoiMouseUpCommand = new DelegateCommand<object>(ExecuteRoiMouseUp);
            ClearRoiCommand = new DelegateCommand(ExecuteClearRoi, CanExecuteClearRoi);

            ApplyBinaryEffectCommand = new DelegateCommand(ExecuteApplyBinaryEffect, CanExecuteApplyEffect);
            ApplyGrayscaleEffectCommand = new DelegateCommand(ExecuteApplyGrayscaleEffect, CanExecuteApplyEffect);
            ApplyGaussianBlurEffectCommand = new DelegateCommand(ExecuteApplyGaussianBlurEffect, CanExecuteApplyEffect);
            ApplySharpenEffectCommand = new DelegateCommand(ExecuteApplySharpenEffect, CanExecuteApplyEffect);
            ApplyColorDetectionCommand = new DelegateCommand(ExecuteApplyColorDetection, CanExecuteApplyEffect);
            ClearImageEffectCommand = new DelegateCommand(ExecuteClearImageEffect, CanExecuteApplyEffect);
            ResetBrightnessContrastCommand = new DelegateCommand(ExecuteResetBrightnessContrast);
            SaveSettingsCommand = new DelegateCommand(ExecuteSaveSettings);
            LoadSettingsCommand = new DelegateCommand(ExecuteLoadSettings);
            PreviousFrameCommand = new DelegateCommand(ExecutePreviousFrame, () => CanExecuteFrameNavigation);
            NextFrameCommand = new DelegateCommand(ExecuteNextFrame, () => CanExecuteFrameNavigation);
            SliderDragStartedCommand = new DelegateCommand<object>(ExecuteSliderDragStarted);
            SliderDragCompletedCommand = new DelegateCommand<object>(ExecuteSliderDragCompleted);
            CaptureScreenshotCommand = new DelegateCommand(ExecuteCaptureScreenshot, CanExecuteCaptureScreenshot);
            ToggleRecordingCommand = new DelegateCommand(ExecuteToggleRecording, CanExecuteToggleRecording);
            ClearErrorCommand = new DelegateCommand(ExecuteClearError);

            _frame = new Mat();
            PlayPauseButtonText = "재생";
            PlayPauseIcon = "";
            _frameDelay = 1000 / _targetFps;
            UpdateCommandStates();
        }
        #endregion

        #region --- 비디오 처리 로직 ---

        private (double width, double height, double offsetX, double offsetY) GetActualImageDimensions()
        {
            if (_frame == null || _frame.IsDisposed || _frame.Empty() || ImageRenderWidth <= 0 || ImageRenderHeight <= 0)
                return (0, 0, 0, 0);

            if (_cachedImageDimensions.HasValue &&
                _lastFrameCols == _frame.Cols &&
                _lastFrameRows == _frame.Rows &&
                _lastImageRenderWidth == ImageRenderWidth &&
                _lastImageRenderHeight == ImageRenderHeight)
            {
                return _cachedImageDimensions.Value;
            }

            double imageAspectRatio = (double)_frame.Cols / _frame.Rows;
            double containerAspectRatio = ImageRenderWidth / ImageRenderHeight;
            double actualWidth, actualHeight, offsetX = 0, offsetY = 0;

            if (imageAspectRatio > containerAspectRatio)
            {
                actualWidth = ImageRenderWidth;
                actualHeight = ImageRenderWidth / imageAspectRatio;
                offsetY = (ImageRenderHeight - actualHeight) / 2;
            }
            else
            {
                actualHeight = ImageRenderHeight;
                actualWidth = ImageRenderHeight * imageAspectRatio;
                offsetX = (ImageRenderWidth - actualWidth) / 2;
            }

            _lastFrameCols = _frame.Cols;
            _lastFrameRows = _frame.Rows;
            _lastImageRenderWidth = ImageRenderWidth;
            _lastImageRenderHeight = ImageRenderHeight;
            _cachedImageDimensions = (actualWidth, actualHeight, offsetX, offsetY);

            return _cachedImageDimensions.Value;
        }

        private async void ProcessVideo(CancellationToken token)
        {
            int consecutiveErrors = 0;
            const int maxConsecutiveErrors = 5;
            
            try
            {
                while (_capture != null && _capture.IsOpened() && !token.IsCancellationRequested)
                {
                    if (!_isPaused)
                    {
                        try
                        {
                            await ProcessFrame(token);
                            consecutiveErrors = 0; // 성공하면 에러 카운트 리셋
                        }
                        catch (Exception frameEx)
                        {
                            consecutiveErrors++;
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                StatusText = $"프레임 처리 오류 ({consecutiveErrors}/{maxConsecutiveErrors}): {frameEx.Message}";
                                if (consecutiveErrors >= maxConsecutiveErrors - 1)
                                {
                                    ShowError($"비디오 처리 중 연속적인 오류가 발생했습니다: {frameEx.Message}");
                                }
                            });
                            
                            // 연속적인 에러가 임계치를 초과하면 자동 중지
                            if (consecutiveErrors >= maxConsecutiveErrors)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    string errorMessage = $"비디오 처리 중 연속적인 오류가 발생했습니다.\n\n" +
                                                        $"오류: {frameEx.Message}\n\n" +
                                                        $"자동으로 재생을 중지합니다.";
                                    
                                    MessageBox.Show(errorMessage, "비디오 처리 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                                    StopPlayback();
                                });
                                return;
                            }
                            
                            // 짧은 대기 후 다시 시도
                            await Task.Delay(100, token);
                        }
                    }
                    await Task.Delay(_frameDelay, token);
                }
            }
            catch (TaskCanceledException) { /* Normal cancellation */ }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    string errorMessage = $"비디오 처리 중 예기치 않은 오류가 발생했습니다.\n\n" +
                                        $"오류: {ex.Message}";
                    
                    MessageBox.Show(errorMessage, "비디오 처리 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusText = $"비디오 처리 오류: {ex.Message}";
                    StopPlayback();
                });
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                    {
                        StatusText = "재생이 중지되었습니다.";
                    }
                    CleanupCapture();
                    UpdateCommandStates();
                });
            }
        }

        private bool _isFirstFrame = true;
        
        private async Task ProcessFrame(CancellationToken token)
        {
            if (_disposed) return;
            
            _frameStopwatch.Restart();
            
            using (Mat tempFrame = new Mat())
            {
                try
                {
                    if (_capture.Read(tempFrame) && !tempFrame.Empty())
                    {
                        // 안전한 프레임 교체
                        Mat newFrame = null;
                        try
                        {
                            newFrame = tempFrame.Clone();
                            
                            // 기존 프레임을 안전하게 교체
                            Mat oldFrame = _frame;
                            _frame = newFrame;
                            oldFrame?.Dispose();
                        }
                        catch
                        {
                            // Clone 실패 시 새 프레임 정리
                            newFrame?.Dispose();
                            throw;
                        }
                        
                        // 첫 번째 프레임 로드 후 커맨드 상태 업데이트
                        if (_isFirstFrame)
                        {
                            _isFirstFrame = false;
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                CaptureScreenshotCommand.RaiseCanExecuteChanged();
                                ToggleRecordingCommand.RaiseCanExecuteChanged();
                            });
                        }
                        
                        // 비디오 파일의 프레임 위치 업데이트 (슬라이더 드래그 중에는 제외)
                        if (_isVideoFile && !_isSliderBeingDragged)
                        {
                            CurrentFramePosition = (int)_capture.Get(VideoCaptureProperties.PosFrames);
                            UpdateFrameInfo();
                        }

                        using (Mat displayFrame = _frame.Clone())
                        {
                            try
                            {
                                ProcessRoiEffects(displayFrame);
                            }
                            catch (Exception roiEx)
                            {
                                // ROI 처리 중 오류가 발생해도 원본 프레임은 표시
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    StatusText = $"ROI 효과 처리 오류: {roiEx.Message}";
                                    ShowError($"ROI 효과 처리 중 오류가 발생했습니다: {roiEx.Message}");
                                });
                            }

                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                VideoFrame = BitmapSourceConverter.ToBitmapSource(displayFrame);
                            }, System.Windows.Threading.DispatcherPriority.Render, token);
                            
                            // Write frame to video file if recording
                            if (_isRecording && _videoWriter != null && _videoWriter.IsOpened())
                            {
                                try
                                {
                                    _videoWriter.Write(displayFrame);
                                }
                                catch (Exception recordEx)
                                {
                                    // 녹화 오류 발생 시 녹화 중지
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        StatusText = $"녹화 오류: {recordEx.Message}";
                                        ShowError($"녹화 중 오류가 발생했습니다: {recordEx.Message}");
                                        StopRecording();
                                    });
                                }
                            }
                        }
                        
                        // Update performance metrics
                        _frameStopwatch.Stop();
                        CurrentProcessingTime = _frameStopwatch.Elapsed.TotalMilliseconds;
                        
                        _frameCount++;
                        if (_fpsStopwatch.ElapsedMilliseconds >= 1000) // Update FPS every second
                        {
                            CurrentFps = _frameCount / (_fpsStopwatch.ElapsedMilliseconds / 1000.0);
                            _frameCount = 0;
                            _fpsStopwatch.Restart();
                        }
                        
                        UpdatePerformanceInfo();
                    }
                    else
                    {
                        // 비디오 파일의 경우 끝에 도달한 것일 수 있음
                        if (_isVideoFile)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                StatusText = "비디오 재생 완료";
                                StopPlayback();
                            });
                        }
                        else
                        {
                            // 웹캠의 경우 읽기 오류
                            throw new Exception("프레임을 읽을 수 없습니다.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 프레임 처리 중 발생한 오류를 상위로 전달
                    throw new Exception($"프레임 처리 오류: {ex.Message}", ex);
                }
            }
        }

        private void ProcessRoiEffects(Mat displayFrame)
        {
            if (_disposed) return;
            
            try
            {
                if (!IsRoiDefined() || ImageRenderWidth <= 0 || ImageRenderHeight <= 0) return;

                var (actualImageWidth, actualImageHeight, roiOffsetX, roiOffsetY) = GetActualImageDimensions();
                if (actualImageWidth <= 0 || actualImageHeight <= 0) return;

                double scaleX = displayFrame.Cols / actualImageWidth;
                double scaleY = displayFrame.Rows / actualImageHeight;

                OpenCvSharp.Rect roiCvScaled = GetScaledRoi(scaleX, scaleY, roiOffsetX, roiOffsetY);
                roiCvScaled.X = Math.Max(0, Math.Min(roiCvScaled.X, displayFrame.Cols - 1));
                roiCvScaled.Y = Math.Max(0, Math.Min(roiCvScaled.Y, displayFrame.Rows - 1));
                if (roiCvScaled.X + roiCvScaled.Width > displayFrame.Cols) roiCvScaled.Width = displayFrame.Cols - roiCvScaled.X;
                if (roiCvScaled.Y + roiCvScaled.Height > displayFrame.Rows) roiCvScaled.Height = displayFrame.Rows - roiCvScaled.Y;

                if (roiCvScaled.Width > 0 && roiCvScaled.Height > 0)
                {
                    ApplyImageEffect(displayFrame, roiCvScaled);
                    
                    // Brightness and Contrast must be applied AFTER other effects.
                    ApplyBrightnessAndContrast(displayFrame, roiCvScaled);

                    displayFrame.Rectangle(roiCvScaled, Scalar.LimeGreen, 2);
                }
            }
            catch (Exception ex)
            {
                // ROI 처리 중 오류가 발생해도 계속 진행
                System.Diagnostics.Debug.WriteLine($"ProcessRoiEffects 오류: {ex.Message}");
                throw; // 상위로 오류 전달
            }
        }

        #endregion

        #region --- 이미지 효과 로직 ---

        private void ApplyBrightnessAndContrast(Mat displayFrame, OpenCvSharp.Rect roiRect)
        {
            // Do nothing if values are at default (with tolerance for floating point)
            if (Math.Abs(Brightness) < 0.01 && Math.Abs(Contrast - 1.0) < 0.01) return;

            using (Mat roiSection = new Mat(displayFrame, roiRect))
            {
                // ConvertTo performs: output = input * alpha + beta
                // We adjust beta to make contrast work around the midpoint (128)
                // New formula: output = input * Contrast + (Brightness - 128 * (Contrast - 1.0))
                double alpha = Contrast;
                double beta = Brightness - 128.0 * (Contrast - 1.0);
                
                roiSection.ConvertTo(roiSection, -1, alpha, beta);
            }
        }

        private void ApplyImageEffect(Mat displayFrame, OpenCvSharp.Rect roiRect)
        {
            if (CurrentImageEffect == ImageEffectType.None) return;

            try
            {
                using (Mat roiSection = new Mat(displayFrame, roiRect))
                {
                    switch (CurrentImageEffect)
                    {
                        case ImageEffectType.Binary: ApplyBinaryEffect(roiSection); break;
                        case ImageEffectType.Grayscale: ApplyGrayscaleEffect(roiSection); break;
                        case ImageEffectType.GaussianBlur: ApplyGaussianBlurEffect(roiSection); break;
                        case ImageEffectType.Sharpen: ApplySharpenEffect(roiSection); break;
                        case ImageEffectType.ColorDetection: ApplyColorDetectionEffect(roiSection); break;
                    }
                }
            }
            catch (Exception ex)
            {
                // 이미지 효과 처리 실패 시 기본 이미지 사용
                System.Diagnostics.Debug.WriteLine($"ApplyImageEffect 오류 ({CurrentImageEffect}): {ex.Message}");
                throw new Exception($"{CurrentImageEffect} 효과 처리 실패", ex);
            }
        }

        private void ApplyBinaryEffect(Mat roiSection)
        {
            using (var grayRoi = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC1))
            using (var binaryRoi = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC1))
            using (var binaryRoiForCopy = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC3))
            {
                Cv2.CvtColor(roiSection, grayRoi.Mat, ColorConversionCodes.BGR2GRAY);
                Cv2.Threshold(grayRoi.Mat, binaryRoi.Mat, (int)BinaryThreshold, 255, ThresholdTypes.Binary);
                Cv2.CvtColor(binaryRoi.Mat, binaryRoiForCopy.Mat, ColorConversionCodes.GRAY2BGR);
                binaryRoiForCopy.Mat.CopyTo(roiSection);
            }
        }

        private void ApplyGrayscaleEffect(Mat roiSection)
        {
            using (var grayRoi = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC1))
            using (var grayRoiForCopy = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC3))
            {
                Cv2.CvtColor(roiSection, grayRoi.Mat, ColorConversionCodes.BGR2GRAY);
                Cv2.CvtColor(grayRoi.Mat, grayRoiForCopy.Mat, ColorConversionCodes.GRAY2BGR);
                grayRoiForCopy.Mat.CopyTo(roiSection);
            }
        }

        private void ApplyGaussianBlurEffect(Mat roiSection)
        {
            using (var blurredRoi = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, roiSection.Type()))
            {
                int kernelSize = ((int)BlurStrength / 2) * 2 + 1; // 홀수로 만들기
                Cv2.GaussianBlur(roiSection, blurredRoi.Mat, new OpenCvSharp.Size(kernelSize, kernelSize), 0);
                blurredRoi.Mat.CopyTo(roiSection);
            }
        }
        private void ApplySharpenEffect(Mat roiSection)
        {
            using (var blurred = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, roiSection.Type()))
            using (var sharpenedResult = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, roiSection.Type()))
            {
                Cv2.GaussianBlur(roiSection, blurred.Mat, new OpenCvSharp.Size(3, 3), 1.0);
                Cv2.AddWeighted(roiSection, 1 + SharpenStrength, blurred.Mat, -SharpenStrength, 0, sharpenedResult.Mat);
                sharpenedResult.Mat.CopyTo(roiSection);
            }
        }
        
        private void ApplyColorDetectionEffect(Mat roiSection)
        {
            using (var hsvRoi = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC3))
            using (var mask = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC1))
            using (var result = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC3))
            {
                // HSV 색 공간으로 변환
                Cv2.CvtColor(roiSection, hsvRoi.Mat, ColorConversionCodes.BGR2HSV);
                
                // HSV 범위 생성
                Scalar lowerBound = new Scalar(HueLower, SaturationLower, ValueLower);
                Scalar upperBound = new Scalar(HueUpper, SaturationUpper, ValueUpper);
                
                // 색상 검출을 위한 마스크 생성
                Cv2.InRange(hsvRoi.Mat, lowerBound, upperBound, mask.Mat);
                
                // 노이즈 감소를 위한 형태학적 연산 적용
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5)))
                {
                    Cv2.MorphologyEx(mask.Mat, mask.Mat, MorphTypes.Open, kernel);
                    Cv2.MorphologyEx(mask.Mat, mask.Mat, MorphTypes.Close, kernel);
                }
                
                // 검출된 영역에 색상 오버레이 생성
                using (var colorOverlay = _matPool.RentScoped(roiSection.Rows, roiSection.Cols, MatType.CV_8UC3))
                {
                    colorOverlay.Mat.SetTo(new Scalar(0, 255, 0)); // 녹색 오버레이
                    colorOverlay.Mat.CopyTo(result.Mat, mask.Mat);
                    
                    // 오버레이를 원본 이미지와 혼합
                    Cv2.AddWeighted(roiSection, 0.7, result.Mat, 0.3, 0, roiSection);
                }
                
                // 선택사항: 검출된 영역 주위에 윤곽선 그리기
                using (var contourMask = _matPool.RentScoped())
                {
                    mask.Mat.CopyTo(contourMask.Mat);
                    OpenCvSharp.Point[][] contours;
                    HierarchyIndex[] hierarchy;
                    Cv2.FindContours(contourMask.Mat, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
                    
                    // 윤곽선 그리기
                    Cv2.DrawContours(roiSection, contours, -1, new Scalar(0, 255, 255), 2); // 노란색 윤곽선
                }
            }
        }
        #endregion

        #region --- 스레드 안전 ROI 접근자 ---
        
        private bool IsRoiDefined()
        {
            lock (_roiLock) { return CurrentRoi.IsDefined; }
        }

        private OpenCvSharp.Rect GetScaledRoi(double scaleX, double scaleY, double offsetX, double offsetY)
        {
            lock (_roiLock)
            {
                double adjustedX = CurrentRoi.X - offsetX;
                double adjustedY = CurrentRoi.Y - offsetY;
                return new OpenCvSharp.Rect(
                    (int)Math.Round(adjustedX * scaleX),
                    (int)Math.Round(adjustedY * scaleY),
                    (int)Math.Round(CurrentRoi.Width * scaleX),
                    (int)Math.Round(CurrentRoi.Height * scaleY)
                );
            }
        }
        #endregion

        #region --- 커맨드 핸들러 ---

        private void ExecuteStartWebcam()
        {
            if (_disposed) return;
            InitializeVideoCapture(0);
        }
        private bool CanExecuteStartWebcam() => !_isPlaying && !_disposed;

        private void ExecuteOpenFile()
        {
            if (_disposed) return;
            
            var openFileDialog = new OpenFileDialog 
            { 
                Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.mpg;*.mpeg|All files|*.*",
                Title = "비디오 파일 선택"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                InitializeVideoCapture(openFileDialog.FileName);
            }
        }
        private bool CanExecuteOpenFile() => !_isPlaying && !_disposed;
        private void InitializeVideoCapture(object source)
        {
            if (_disposed) return;
            
            try
            {
                if (_isPlaying || _isPaused) StopPlayback();

                if (source is int deviceId)
                {
                    // 웹캠이 실제로 존재하는지 먼저 확인
                    var testCapture = new VideoCapture(deviceId, VideoCaptureAPIs.DSHOW);
                    if (!testCapture.IsOpened())
                    {
                        testCapture?.Dispose();
                        // 더 자세한 오류 메시지 제공
                        string errorMessage = $"웹캠을 열 수 없습니다.\n\n" +
                                           $"다음 사항을 확인해주세요:\n" +
                                           $"• 웹캠이 컴퓨터에 연결되어 있는지 확인\n" +
                                           $"• 다른 프로그램에서 웹캠을 사용 중이 아닌지 확인\n" +
                                           $"• 웹캠 드라이버가 올바르게 설치되어 있는지 확인\n" +
                                           $"• Windows 설정에서 카메라 접근 권한이 허용되어 있는지 확인";
                        
                        StatusText = "웹캠 연결 실패";
                        
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            ShowError(errorMessage);
                            MessageBox.Show(errorMessage, "웹캠 연결 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        
                        UpdateCommandStates();
                        return;
                    }
                    
                    // 실제 프레임을 읽을 수 있는지 테스트
                    using (var testFrame = new Mat())
                    {
                        if (!testCapture.Read(testFrame) || testFrame.Empty())
                        {
                            testCapture?.Dispose();
                            string errorMessage = "웹캠이 연결되었지만 영상을 읽을 수 없습니다.\n" +
                                                "가상 카메라나 OBS를 사용 중이라면 해당 프로그램이 실행 중인지 확인하세요.";
                            
                            StatusText = "웹캠 영상 읽기 실패";
                            
                            Application.Current.Dispatcher.Invoke(() => 
                            {
                                ShowError(errorMessage);
                                MessageBox.Show(errorMessage, "웹캠 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                            
                            UpdateCommandStates();
                            return;
                        }
                    }
                    
                    testCapture?.Dispose();
                    _capture = new VideoCapture(deviceId, VideoCaptureAPIs.DSHOW);
                    StatusText = $"웹캠 {deviceId} 연결 시도 중...";
                    _isVideoFile = false;
                    if (!_capture.IsOpened())
                    {
                        // 더 자세한 오류 메시지 제공
                        string errorMessage = $"웹캠을 열 수 없습니다.\n\n" +
                                           $"다음 사항을 확인해주세요:\n" +
                                           $"• 웹캠이 컴퓨터에 연결되어 있는지 확인\n" +
                                           $"• 다른 프로그램에서 웹캠을 사용 중이 아닌지 확인\n" +
                                           $"• 웹캠 드라이버가 올바르게 설치되어 있는지 확인\n" +
                                           $"• Windows 설정에서 카메라 접근 권한이 허용되어 있는지 확인";
                        
                        StatusText = "웹캠 연결 실패";
                        
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            ShowError(errorMessage);
                            MessageBox.Show(errorMessage, "웹캠 연결 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        
                        CleanupCapture();
                        UpdateCommandStates();
                        return;
                    }
                }
                else if (source is string filePath)
                {
                    // 지원되는 파일 형식 확인
                    string[] supportedFormats = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".mpg", ".mpeg" };
                    string fileExtension = Path.GetExtension(filePath).ToLower();
                    
                    if (!supportedFormats.Contains(fileExtension))
                    {
                        string supportedFormatsMessage = $"지원되지 않는 파일 형식입니다.\n\n" +
                                                       $"현재 파일: {Path.GetFileName(filePath)}\n" +
                                                       $"파일 형식: {fileExtension}\n\n" +
                                                       $"지원되는 형식:\n" + 
                                                       string.Join(", ", supportedFormats);
                        
                        StatusText = "파일 형식 오류";
                        
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            ShowError(supportedFormatsMessage);
                            MessageBox.Show(supportedFormatsMessage, "파일 형식 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        
                        UpdateCommandStates();
                        return;
                    }
                    
                    _capture = new VideoCapture(filePath);
                    StatusText = $"파일 열기: {Path.GetFileName(filePath)}";
                    _isVideoFile = true;
                    
                    // Get video information for frame navigation
                    if (_capture.IsOpened())
                    {
                        TotalFrameCount = (int)_capture.Get(VideoCaptureProperties.FrameCount);
                        CurrentFramePosition = 0;
                        UpdateFrameInfo();
                    }
                    else
                    {
                        // 파일을 열 수 없는 경우
                        string fileOpenErrorMessage = $"비디오 파일을 열 수 없습니다.\n\n" +
                                                    $"파일: {Path.GetFileName(filePath)}\n\n" +
                                                    $"가능한 원인:\n" +
                                                    $"• 파일이 손상되었거나 이동/삭제되었음\n" +
                                                    $"• 필요한 코덱이 설치되지 않음\n" +
                                                    $"• 파일 접근 권한이 없음";
                        
                        StatusText = "파일 열기 실패";
                        
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            ShowError(fileOpenErrorMessage);
                            MessageBox.Show(fileOpenErrorMessage, "파일 열기 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        
                        CleanupCapture();
                        UpdateCommandStates();
                        return;
                    }
                }

                if (_capture == null || !_capture.IsOpened())
                {
                    StatusText = "비디오 소스를 열 수 없습니다.";
                    CleanupCapture();
                    UpdateCommandStates();
                    return;
                }

                _isPlaying = true;
                _isPaused = false;
                PlayPauseButtonText = "일시정지";
                PlayPauseIcon = "";
                StatusText = "재생 중...";
                RaisePropertyChanged(nameof(CanExecuteFrameNavigation));
                _cancellationTokenSource = new CancellationTokenSource();
                _videoProcessingTask = Task.Run(() => ProcessVideo(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                
                // Update screenshot and recording commands
                CaptureScreenshotCommand.RaiseCanExecuteChanged();
                ToggleRecordingCommand.RaiseCanExecuteChanged();
                
                // Start performance monitoring
                _fpsStopwatch.Restart();
                _frameCount = 0;
            }
            catch (Exception ex)
            {
                StatusText = $"오류: {ex.Message}";
                CleanupCapture();
            }
            finally
            {
                UpdateCommandStates();
                // Ensure screenshot and recording commands are updated after video loads
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CaptureScreenshotCommand.RaiseCanExecuteChanged();
                    ToggleRecordingCommand.RaiseCanExecuteChanged();
                });
            }
        }

        private void ExecutePlayPause()
        {
            if (!_isPlaying && !_isPaused)
            {
                if (_capture != null && _capture.IsOpened())
                {
                    _isPlaying = true;
                    _isPaused = false;
                    PlayPauseButtonText = "일시정지";
                    PlayPauseIcon = "";
                    StatusText = "재생 중...";
                    if (_videoProcessingTask == null || _videoProcessingTask.IsCompleted)
                    {
                        _cancellationTokenSource = new CancellationTokenSource();
                        _videoProcessingTask = Task.Run(() => ProcessVideo(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                    }
                }
                else StatusText = "재생할 비디오 소스가 없습니다.";
            }
            else
            {
                _isPaused = !_isPaused;
                PlayPauseButtonText = _isPaused ? "재생" : "일시정지";
                PlayPauseIcon = _isPaused ? "" : "";
                StatusText = _isPaused ? "일시정지됨" : "재생 중...";
                RaisePropertyChanged(nameof(CanExecuteFrameNavigation));
                
                // Update screenshot and recording commands when pause state changes
                CaptureScreenshotCommand.RaiseCanExecuteChanged();
                ToggleRecordingCommand.RaiseCanExecuteChanged();
            }
            UpdateCommandStates();
        }
        private bool CanExecutePlayPause() => _capture != null && _capture.IsOpened();

        private void ExecuteStop() => StopPlayback();
        private bool CanExecuteStop() => _isPlaying || _isPaused;

        private async void StopPlayback()
        {
            _isPlaying = false;
            _isPaused = false;
            _cancellationTokenSource?.Cancel();

            if (_videoProcessingTask != null && !_videoProcessingTask.IsCompleted)
            {
                try { await _videoProcessingTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { /* Expected */ }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                PlayPauseButtonText = "재생";
                PlayPauseIcon = "";
                StatusText = "정지됨";
                VideoFrame = null;
                RaisePropertyChanged(nameof(CanExecuteFrameNavigation));
                UpdateCommandStates();
            });
        }
        
        // --- ROI 커맨드 핸들러 ---
        private void ExecuteRoiMouseDown(object args)
        {
            if (args is MouseButtonEventArgs e && e.OriginalSource is FrameworkElement imageControl && e.LeftButton == MouseButtonState.Pressed)
            {
                if (_isRoiDrawing || (!_isPlaying && !_isPaused)) return;
                
                ImageRenderWidth = imageControl.ActualWidth;
                ImageRenderHeight = imageControl.ActualHeight;

                _roiStartPoint = e.GetPosition(imageControl);
                lock (_roiLock)
                {
                    CurrentRoi.X = Math.Round(_roiStartPoint.X);
                    CurrentRoi.Y = Math.Round(_roiStartPoint.Y);
                    CurrentRoi.Width = 0;
                    CurrentRoi.Height = 0;
                }
                _isRoiDrawing = true;
                imageControl.CaptureMouse();
                StatusText = $"ROI 시작: ({CurrentRoi.X:F0}, {CurrentRoi.Y:F0})";
            }
        }

        private void ExecuteRoiMouseMove(object args)
        {
            if (_isRoiDrawing && args is MouseEventArgs e && e.OriginalSource is FrameworkElement imageControl)
            {
                System.Windows.Point currentPoint = e.GetPosition(imageControl);
                currentPoint.X = Math.Max(0, Math.Min(currentPoint.X, imageControl.ActualWidth));
                currentPoint.Y = Math.Max(0, Math.Min(currentPoint.Y, imageControl.ActualHeight));

                lock (_roiLock)
                {
                    CurrentRoi.X = Math.Round(Math.Min(_roiStartPoint.X, currentPoint.X));
                    CurrentRoi.Y = Math.Round(Math.Min(_roiStartPoint.Y, currentPoint.Y));
                    CurrentRoi.Width = Math.Round(Math.Abs(_roiStartPoint.X - currentPoint.X));
                    CurrentRoi.Height = Math.Round(Math.Abs(_roiStartPoint.Y - currentPoint.Y));
                }
                StatusText = $"ROI: ({CurrentRoi.X:F0}, {CurrentRoi.Y:F0}) 크기: ({CurrentRoi.Width:F0}x{CurrentRoi.Height:F0})";
            }
        }

        private void ExecuteRoiMouseUp(object args)
        {
            if (_isRoiDrawing && args is MouseButtonEventArgs e && e.OriginalSource is FrameworkElement imageControl)
            {
                _isRoiDrawing = false;
                imageControl.ReleaseMouseCapture();

                lock (_roiLock)
                {
                    if (CurrentRoi.Width < 5 || CurrentRoi.Height < 5)
                    {
                        CurrentRoi.Reset();
                        StatusText = "ROI가 너무 작아 초기화되었습니다.";
                    }
                    else
                    {
                        StatusText = $"ROI 설정 완료: ({CurrentRoi.X:F0}, {CurrentRoi.Y:F0}) 크기: ({CurrentRoi.Width:F0}x{CurrentRoi.Height:F0})";
                    }
                }
                ClearRoiCommand.RaiseCanExecuteChanged();
            }
        }

        private void ExecuteClearRoi()
        {
            lock (_roiLock) { CurrentRoi.Reset(); }
            StatusText = "ROI가 초기화되었습니다.";
        }
        private bool CanExecuteClearRoi() => IsRoiDefined();

        // --- 효과 커맨드 핸들러 ---
        private void ExecuteApplyBinaryEffect() => SetImageEffect(ImageEffectType.Binary, "흑백");
        private void ExecuteApplyGrayscaleEffect() => SetImageEffect(ImageEffectType.Grayscale, "그레이스케일");
        private void ExecuteApplyGaussianBlurEffect() => SetImageEffect(ImageEffectType.GaussianBlur, "가우시안 블러");
        private void ExecuteApplySharpenEffect() => SetImageEffect(ImageEffectType.Sharpen, "샤프닝");
        private void ExecuteApplyColorDetection() => SetImageEffect(ImageEffectType.ColorDetection, "색상 검출");
        private void ExecuteClearImageEffect() => SetImageEffect(ImageEffectType.None, "효과 없음");
        private bool CanExecuteApplyEffect() => _isPlaying || _isPaused;

        private void SetImageEffect(ImageEffectType effect, string effectName)
        {
            CurrentImageEffect = effect;
            StatusText = $"{effectName} 효과 적용됨 (ROI 내부)";
        }

        private void ExecuteResetBrightnessContrast()
        {
            Brightness = 0;
            Contrast = 1.0;
            StatusText = "밝기/대비가 기본값으로 초기화되었습니다.";
        }
        
        // --- 설정 저장/불러오기 커맨드 핸들러 ---
        private async void ExecuteSaveSettings()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files|*.json|All files|*.*",
                DefaultExt = "json",
                FileName = $"RealTimeVideoAnalysis_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                var settings = new AppSettings
                {
                    RoiSettings = new RoiSettings
                    {
                        X = CurrentRoi.X,
                        Y = CurrentRoi.Y,
                        Width = CurrentRoi.Width,
                        Height = CurrentRoi.Height,
                        IsDefined = CurrentRoi.IsDefined
                    },
                    ImageEffectSettings = new ImageEffectSettings
                    {
                        CurrentEffect = CurrentImageEffect.ToString(),
                        BinaryThreshold = BinaryThreshold,
                        BlurStrength = BlurStrength,
                        SharpenStrength = SharpenStrength
                    },
                    AdjustmentSettings = new AdjustmentSettings
                    {
                        Brightness = Brightness,
                        Contrast = Contrast,
                        TargetFps = TargetFps
                    },
                    ColorDetectionSettings = new ColorDetectionSettings
                    {
                        HueLower = HueLower,
                        HueUpper = HueUpper,
                        SaturationLower = SaturationLower,
                        SaturationUpper = SaturationUpper,
                        ValueLower = ValueLower,
                        ValueUpper = ValueUpper
                    }
                };
                
                bool success = await _settingsService.SaveSettingsAsync(settings, saveFileDialog.FileName);
                StatusText = success ? "설정이 저장되었습니다." : "설정 저장에 실패했습니다.";
            }
        }
        
        private async void ExecuteLoadSettings()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON Files|*.json|All files|*.*"
            };
            
            if (openFileDialog.ShowDialog() == true)
            {
                var settings = await _settingsService.LoadSettingsAsync(openFileDialog.FileName);
                
                if (settings != null)
                {
                    // Apply ROI settings
                    lock (_roiLock)
                    {
                        CurrentRoi.X = settings.RoiSettings.X;
                        CurrentRoi.Y = settings.RoiSettings.Y;
                        CurrentRoi.Width = settings.RoiSettings.Width;
                        CurrentRoi.Height = settings.RoiSettings.Height;
                        // IsDefined is read-only and automatically calculated from Width and Height
                    }
                    
                    // Apply image effect settings
                    if (Enum.TryParse<ImageEffectType>(settings.ImageEffectSettings.CurrentEffect, out var effect))
                    {
                        CurrentImageEffect = effect;
                    }
                    BinaryThreshold = settings.ImageEffectSettings.BinaryThreshold;
                    BlurStrength = settings.ImageEffectSettings.BlurStrength;
                    SharpenStrength = settings.ImageEffectSettings.SharpenStrength;
                    
                    // Apply adjustment settings
                    Brightness = settings.AdjustmentSettings.Brightness;
                    Contrast = settings.AdjustmentSettings.Contrast;
                    TargetFps = settings.AdjustmentSettings.TargetFps;
                    
                    // Apply color detection settings
                    HueLower = settings.ColorDetectionSettings.HueLower;
                    HueUpper = settings.ColorDetectionSettings.HueUpper;
                    SaturationLower = settings.ColorDetectionSettings.SaturationLower;
                    SaturationUpper = settings.ColorDetectionSettings.SaturationUpper;
                    ValueLower = settings.ColorDetectionSettings.ValueLower;
                    ValueUpper = settings.ColorDetectionSettings.ValueUpper;
                    
                    StatusText = $"설정을 불러왔습니다. (저장 시간: {settings.SavedAt:yyyy-MM-dd HH:mm:ss})";
                }
                else
                {
                    StatusText = "설정 파일을 불러올 수 없습니다.";
                }
            }
        }
        
        // --- 프레임 탐색 커맨드 핸들러 ---
        private void ExecutePreviousFrame()
        {
            if (_capture != null && _isVideoFile && _isPaused)
            {
                int newPosition = Math.Max(0, CurrentFramePosition - 1);
                SeekToFrame(newPosition);
            }
        }
        
        private void ExecuteNextFrame()
        {
            if (_capture != null && _isVideoFile && _isPaused)
            {
                int newPosition = Math.Min(TotalFrameCount - 1, CurrentFramePosition + 1);
                SeekToFrame(newPosition);
            }
        }
        
        private void SeekToFrame(int frameNumber)
        {
            if (_capture != null && _capture.IsOpened())
            {
                _capture.Set(VideoCaptureProperties.PosFrames, frameNumber);
                
                using (Mat tempFrame = new Mat())
                {
                    if (_capture.Read(tempFrame) && !tempFrame.Empty())
                    {
                        _frame?.Dispose();
                        _frame = tempFrame.Clone();
                        CurrentFramePosition = frameNumber;
                        UpdateFrameInfo();
                        
                        using (Mat displayFrame = _frame.Clone())
                        {
                            ProcessRoiEffects(displayFrame);
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                VideoFrame = BitmapSourceConverter.ToBitmapSource(displayFrame);
                            });
                        }
                    }
                }
            }
        }
        
        private void UpdateFrameInfo()
        {
            if (_isVideoFile)
            {
                FrameInfo = $"프레임: {CurrentFramePosition} / {TotalFrameCount}";
            }
            else
            {
                FrameInfo = "실시간 스트리밍";
            }
        }
        
        private void ExecuteSliderDragStarted(object args)
        {
            _isSliderBeingDragged = true;
        }
        
        private void ExecuteSliderDragCompleted(object args)
        {
            _isSliderBeingDragged = false;
            // 드래그 완료 시 최종 탐색
            if (_capture != null && _isVideoFile && _isPaused)
            {
                SeekToFrame(CurrentFramePosition);
            }
        }
        
        // --- 스크린샷 및 녹화 커맨드 핸들러 ---
        private void ExecuteCaptureScreenshot()
        {
            if (_frame == null || _frame.IsDisposed || _frame.Empty())
            {
                StatusText = "현재 프레임이 없습니다.";
                return;
            }
            
            try
            {
                // Screenshots 디렉토리가 없으면 생성
                string screenshotsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RealTimeVideoAnalysis", "Screenshots");
                Directory.CreateDirectory(screenshotsDir);
                
                // 타임스탬프가 포함된 파일명 생성
                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string filePath = Path.Combine(screenshotsDir, fileName);
                
                // ROI 효과가 적용된 현재 프레임의 복사본 생성
                using (Mat saveFrame = _frame.Clone())
                {
                    ProcessRoiEffects(saveFrame);
                    
                    // 이미지 저장
                    bool success = Cv2.ImWrite(filePath, saveFrame);
                    
                    if (success)
                    {
                        StatusText = $"스크린샷 저장됨: {fileName}";
                    }
                    else
                    {
                        StatusText = "스크린샷 저장 실패";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"스크린샷 저장 오류: {ex.Message}";
            }
        }
        
        private bool CanExecuteCaptureScreenshot() => (_isPlaying || _isPaused) && _frame != null && !_frame.IsDisposed && !_frame.Empty();
        
        private void ExecuteToggleRecording()
        {
            if (!_isRecording)
            {
                StartRecording();
            }
            else
            {
                StopRecording();
            }
        }
        
        private bool CanExecuteToggleRecording() => (_isPlaying || _isPaused) && _frame != null && !_frame.IsDisposed && !_frame.Empty();
        
        private void StartRecording()
        {
            if (_frame == null || _frame.IsDisposed || _frame.Empty())
            {
                StatusText = "녹화할 프레임이 없습니다.";
                return;
            }
            
            try
            {
                // Videos 디렉토리가 없으면 생성
                string videosDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "RealTimeVideoAnalysis");
                Directory.CreateDirectory(videosDir);
                
                // 타임스탬프가 포함된 파일명 생성
                string fileName = $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                _recordingFilePath = Path.Combine(videosDir, fileName);
                
                // 프레임 속성 가져오기
                double fps = _targetFps;
                int frameWidth = _frame.Cols;
                int frameHeight = _frame.Rows;
                
                // MP4 코덱으로 VideoWriter 생성
                _videoWriter = new VideoWriter(
                    _recordingFilePath,
                    FourCC.MP4V,  // MP4 코덱
                    fps,
                    new OpenCvSharp.Size(frameWidth, frameHeight),
                    true  // 컬러 영상
                );
                
                if (_videoWriter.IsOpened())
                {
                    _isRecording = true;
                    IsRecordingActive = true;
                    RecordingStatus = $"녹화 중: {fileName}";
                    StatusText = $"녹화 시작: {fileName}";
                }
                else
                {
                    _videoWriter?.Dispose();
                    _videoWriter = null;
                    StatusText = "비디오 초기화 실패";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"녹화 시작 오류: {ex.Message}";
                _videoWriter?.Dispose();
                _videoWriter = null;
            }
        }
        
        private void StopRecording()
        {
            if (_isRecording && _videoWriter != null)
            {
                try
                {
                    _videoWriter.Release();
                    _videoWriter.Dispose();
                    _videoWriter = null;
                    _isRecording = false;
                    IsRecordingActive = false;
                    RecordingStatus = "";
                    
                    StatusText = $"녹화 종료: {Path.GetFileName(_recordingFilePath)}";
                }
                catch (Exception ex)
                {
                    StatusText = $"녹화 종료 오류: {ex.Message}";
                    ShowError($"녹화 종료 중 오류가 발생했습니다: {ex.Message}");
                }
            }
        }
        
        private void ExecuteClearError()
        {
            HasError = false;
            ErrorMessage = "";
        }
        
        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
            
            // 10초 후 자동으로 에러 메시지 숨기기
            Task.Delay(10000).ContinueWith(_ => 
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    if (ErrorMessage == message) // 다른 에러가 표시되지 않았다면
                    {
                        HasError = false;
                        ErrorMessage = "";
                    }
                });
            });
        }
        #endregion

        #region --- 정리 ---
        private void UpdateCommandStates()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StartWebcamCommand.RaiseCanExecuteChanged();
                OpenFileCommand.RaiseCanExecuteChanged();
                PlayPauseCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
                ClearRoiCommand.RaiseCanExecuteChanged();
                ApplyBinaryEffectCommand.RaiseCanExecuteChanged();
                ApplyGrayscaleEffectCommand.RaiseCanExecuteChanged();
                ApplyGaussianBlurEffectCommand.RaiseCanExecuteChanged();
                ApplySharpenEffectCommand.RaiseCanExecuteChanged();
                ApplyColorDetectionCommand.RaiseCanExecuteChanged();
                ClearImageEffectCommand.RaiseCanExecuteChanged();
                ResetBrightnessContrastCommand.RaiseCanExecuteChanged();
                SaveSettingsCommand.RaiseCanExecuteChanged();
                LoadSettingsCommand.RaiseCanExecuteChanged();
                PreviousFrameCommand.RaiseCanExecuteChanged();
                NextFrameCommand.RaiseCanExecuteChanged();
                CaptureScreenshotCommand.RaiseCanExecuteChanged();
                ToggleRecordingCommand.RaiseCanExecuteChanged();
            });
        }

        private void CleanupCapture()
        {
            try
            {
                // 비디오 라이터가 열려 있으면 먼저 닫기
                if (_videoWriter != null && _videoWriter.IsOpened())
                {
                    _videoWriter.Release();
                }
                _videoWriter?.Dispose();
                _videoWriter = null;
                
                // 캡처 해제
                if (_capture != null)
                {
                    if (_capture.IsOpened())
                    {
                        _capture.Release();
                    }
                    _capture.Dispose();
                    _capture = null;
                }
                
                // 프레임 해제
                _frame?.Dispose();
                _frame = new Mat();
                
                // 상태 초기화
                _isPlaying = false;
                _isPaused = false;
                _isRecording = false;
                IsRecordingActive = false;
                RecordingStatus = "";
                _cachedImageDimensions = null;
                _isVideoFile = false;
                CurrentFramePosition = 0;
                TotalFrameCount = 0;
                FrameInfo = "";
                _isFirstFrame = true;
                _frameCount = 0;
                CurrentFps = 0;
                CurrentProcessingTime = 0;
                PerformanceInfo = "";
                _fpsStopwatch.Stop();
                _frameStopwatch.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CleanupCapture 오류: {ex.Message}");
            }
        }
        
        private void UpdatePerformanceInfo()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                PerformanceInfo = $"FPS: {CurrentFps:F1} | 처리시간: {CurrentProcessingTime:F1}ms";
            });
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            lock (_disposeLock)
            {
                if (_disposed) return;
                
                try
                {
                    if (disposing)
                    {
                        // 관리 리소스 해제
                        
                        // 먼저 재생 중지
                        _cancellationTokenSource?.Cancel();
                        
                        // 비디오 처리 태스크 종료 대기
                        if (_videoProcessingTask != null && !_videoProcessingTask.IsCompleted)
                        {
                            try
                            {
                                _videoProcessingTask.Wait(TimeSpan.FromSeconds(2));
                            }
                            catch { /* 무시 */ }
                        }
                        
                        // 녹화 중지
                        if (_isRecording)
                        {
                            StopRecording();
                        }
                        
                        // 리소스 정리
                        CleanupCapture();
                        
                        // MatPool 정리
                        _matPool?.Dispose();
                        
                        // CancellationTokenSource 정리
                        _cancellationTokenSource?.Dispose();
                        _cancellationTokenSource = null;
                        
                        // 타이머 정리
                        _fpsStopwatch?.Stop();
                        _frameStopwatch?.Stop();
                    }
                    
                    // 비관리 리소스는 여기서 해제 (현재는 없음)
                    
                    _disposed = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Dispose 오류: {ex.Message}");
                }
            }
        }
        
        ~MainWindowViewModel()
        {
            Dispose(false);
        }
        #endregion
    }
} 