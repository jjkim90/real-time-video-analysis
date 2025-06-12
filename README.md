# 🎥 Real-Time Video Analysis

OpenCVSharp와 WPF MVVM을 활용한 실시간 영상 처리 데스크톱 애플리케이션

![application-main-window](https://github.com/user-attachments/assets/a5450378-f833-40b0-bfd6-b69c5562a98f)

## 🌟 주요 기능

### 1. 실시간 영상 처리
- 웹캠 및 동영상 파일 지원
- 30 FPS 안정적 처리
- 프레임 단위 탐색

![video-controls](https://github.com/user-attachments/assets/f81abee7-820f-4e18-bb1b-7031bd06fa5c)


### 2. ROI 기반 선택적 처리
- 마우스 드래그로 관심 영역 지정
- 선택 영역에만 효과 적용 (성능 최적화)

![effects-demo](https://github.com/user-attachments/assets/0b588240-21cb-4524-b757-4ced971720e9)

### 3. 다양한 이미지 효과
![all-effects-comparison](https://github.com/user-attachments/assets/5d227994-4886-4fe9-9b90-22ee9d4db0ae)

- Binary / Grayscale / Gaussian Blur
- Sharpen / Color Detection / Brightness & Contrast

### 4. 편의 기능
- 스크린샷 캡처 (PNG)
- 동영상 녹화 (MP4)
- 설정 저장/불러오기 (JSON)

![capture-video-demo](https://github.com/user-attachments/assets/f1676e5f-5c42-4e9a-be30-6d182312f5cc)


## 🛠️ 기술 스택

| 분야 | 기술 | 버전 | 선택 이유 |
|------|------|------|-----------|
| **언어** | C# | 12.0 | 최신 문법으로 간결한 코드 작성 |
| **프레임워크** | .NET | 8.0 | 최신 .NET 8.0 사용 |
| **UI** | WPF | - | 풍부한 UI 표현, 데이터 바인딩 |
| **아키텍처** | MVVM (Prism) | 9.0 | 관심사 분리 |
| **영상처리** | OpenCVSharp4 | 4.11.0 | .NET 네이티브 지원, 고성능 |
| **데이터** | Newtonsoft.Json | 13.0.3 | 설정 저장/로드 |

## 📁 프로젝트 구조

```
RealTimeVideoAnalysis/
├── Models/
│   ├── RoiModel.cs          # ROI 좌표 및 상태 관리
│   ├── AppSettings.cs       # 애플리케이션 설정
│   └── ImageEffectType.cs   # 이미지 효과 열거형
├── ViewModels/
│   └── MainWindowViewModel.cs  # 핵심 비즈니스 로직
├── Views/
│   ├── MainWindow.xaml      # 메인 UI
│   └── MainWindow.xaml.cs   # 코드 비하인드 (최소화)
├── Services/
│   ├── MatPool.cs           # 메모리 최적화 (객체 풀링)
│   └── SettingsService.cs   # 설정 관리
├── CustomControls/
│   └── IconButton.cs        # 재사용 가능한 커스텀 버튼
└── Themes/
    └── Generic.xaml         # 커스텀 컨트롤 스타일
```

## ⚡ 성능 최적화

### Mat 객체 풀링 구현
장시간 실행 시 프레임 드롭 문제를 객체 풀링으로 해결
- **문제**: 30 FPS = 초당 90개 Mat 객체 생성/소멸로 GC 압박
- **해결**: 재사용 가능한 Mat Pool 구현
- **결과**: GC 발생 90% 감소, 2시간+ 안정적 실행

```csharp
// Before: 매 프레임마다 새 객체 생성
using (Mat grayRoi = new Mat()) { ... }

// After: 객체 재사용
using (var grayRoi = _matPool.RentScoped()) { ... }
```

## 🚀 실행 방법

### 실행 파일 다운로드 (권장)
- 📥 [최신 릴리즈 다운로드](https://github.com/...)
- Windows 10 이상에서 바로 실행 가능

### 소스 코드 빌드
```bash
git clone https://github.com/...
cd real-time-video-analysis
# Visual Studio 2022에서 RealTimeVideoAnalysis.sln 열기
# F5로 실행
```

## 📝 라이선스

이 프로젝트는 MIT 라이선스 하에 있습니다.

## 👨‍💻 개발자

**김재준**
- Email: jaejun8613@gmail.com
- GitHub: [@jjkim90](https://github.com/jjkim90)

---

<p align="center">
  이 프로젝트가 도움이 되었다면 ⭐️ Star를 눌러주세요!
</p>
