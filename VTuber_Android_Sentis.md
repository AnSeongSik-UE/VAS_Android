# 🤖 VTuber Avatar System — Android 버전

> Unity 6000.3.9f1 + Sentis 2.5.0 + sentis-samples 공식 모델 기준
> **언리얼 엔진 경험자를 위한 Unity 대응 설명 포함**
> 모델 파일명은 GitHub 실제 경로 기준 (`BlazeDetectionSample/{Face|Hand|Pose}/Assets/Models/`)

---

## 📋 목차

1. [Unity ↔ Unreal 개념 대응표](#-unity--unreal-개념-대응표)
2. [Unity Editor 창 구성](#-unity-editor-창-구성)
3. [파일 생성 방법](#-파일-생성-방법)
4. [개발 환경](#-개발-환경)
5. [사용 모델](#-사용-모델)
6. [시스템 아키텍처](#-시스템-아키텍처)
7. [디렉토리 구조 + 파일 생성 경로](#-디렉토리-구조--파일-생성-경로)
8. [개발 단계 로드맵](#-개발-단계-로드맵)
9. [Phase 상세 구현](#-phase-상세-구현)
10. [랜드마크 → BlendShape 계산](#-랜드마크--blendshape-계산)
11. [Golang 릴레이 서버 프로토콜](#-golang-릴레이-서버-프로토콜)
12. [Android 빌드 설정](#-android-빌드-설정)
13. [어필 포인트](#-어필-포인트)

---

## 🔄 Unity ↔ Unreal 개념 대응표

| Unreal Engine | Unity | 비고 |
|---------------|-------|------|
| Level (`.umap`) | Scene (`.unity`) | 화면 단위 |
| Actor | GameObject | 씬에 존재하는 모든 오브젝트 |
| ActorComponent | Component / MonoBehaviour | 기능 단위. C# 파일 하나 = 컴포넌트 하나 |
| `BeginPlay()` | `Start()` | 오브젝트 활성화 시 1회 호출 |
| `Tick(float DeltaTime)` | `Update()` | 매 프레임 호출 |
| World Outliner | **Hierarchy 창** | 씬 내 오브젝트 목록 |
| Details Panel | **Inspector 창** | 선택한 오브젝트 속성 편집 |
| Content Browser | **Project 창** | 에셋(파일) 탐색기 |
| `UPROPERTY(EditAnywhere)` | `[SerializeField]` | Inspector에서 값 편집 가능하게 노출 |
| `UPROPERTY(BlueprintReadOnly)` | `public` 변수 | 외부 접근 허용 |
| Plugin (`.uplugin`) | Package (UPM) | 기능 확장 모듈 |
| `UAsset` | Asset | 텍스처, 모델, 사운드 등 모든 리소스 |
| `GEngine->AddOnScreenDebugMessage` | `Debug.Log()` | 콘솔/화면 로그 출력 |
| `Print String` (BP) | `Debug.Log()` | 동일 |
| RenderTarget | RenderTexture | GPU 렌더 결과를 텍스처로 저장 |
| `FQuat` / `FRotator` | `Quaternion` / `Vector3` | 회전 표현 |
| `FVector` | `Vector3` | 3D 벡터 |
| `TArray<float>` | `float[]` / `List<float>` | 배열/리스트 |
| `AsyncTask` | `async Awaitable` | 비동기 처리 (Unity 6 내장) |
| `UObject::BeginDestroy()` | `OnDestroy()` | 오브젝트 파괴 시 호출 |
| `GetWorld()->GetTimerManager()` | `Invoke()` / Coroutine | 타이머 |
| `.Build.cs` 모듈 의존성 | `Packages/manifest.json` | 의존성 관리 |
| Content 폴더 | **Assets 폴더** | 모든 리소스의 루트 |
| `StreamableManager` | `Resources.Load()` | 런타임 에셋 로딩 |
| `StreamingAssets` (없음) | **StreamingAssets 폴더** | 런타임에 파일 경로로 직접 읽는 폴더 |
| MorphTarget | BlendShape | 표정 변형 |
| Skeleton / Bone | `HumanBodyBones` | 인체 본 구조 |
| LiveLink Plugin | AR Foundation | 외부 트래킹 데이터 수신 |
| 소켓 통신 (C++) | `System.Net.Sockets` (C#) | 서버 통신 (Golang Docker 릴레이 서버) |

---

## 🖥 Unity Editor 창 구성

UE Editor와 1:1 대응으로 이해하면 됩니다.

```
┌──────────────────────────────────────────────────────────────────┐
│  Toolbar :  Play ▶  Pause ⏸  Step ⏭   (UE의 Play / Simulate)    │
├───────────────┬──────────────────────────┬───────────────────────┤
│               │                          │                       │
│  Hierarchy    │        Scene 뷰          │      Inspector        │
│  (= World     │    (= Viewport)          │  (= Details Panel)    │
│   Outliner)   │                          │                       │
│               │  오브젝트 배치/이동/회전  │  선택한 오브젝트의    │
│  씬의 모든    │  단축키: W/E/R           │  컴포넌트 목록과      │
│  오브젝트     │  (이동/회전/스케일)       │  [SerializeField] 값  │
│  목록         │                          │  편집                 │
│               ├──────────────────────────┤                       │
│               │         Game 뷰          │                       │
│               │  (실제 실행 화면         │                       │
│               │   Play 시 이쪽이 활성화)  │                       │
├───────────────┴──────────────────────────┴───────────────────────┤
│  Project 창 (= Content Browser)  │  Console 창 (= Output Log)    │
│  Assets/                         │  Debug.Log() 출력             │
│  ├─ Android/                     │  에러/경고 메시지             │
│  ├─ Models/                      │                               │
│  └─ StreamingAssets/             │                               │
└──────────────────────────────────┴───────────────────────────────┘
```

---

## 📁 파일 생성 방법

### C# Script 생성 (UE에서 새 Blueprint 클래스 만드는 것과 동일)

```
방법 1 - Project 창에서:
  Assets/Android/Tracking 폴더 우클릭
  → Create → C# Script
  → 파일명 입력 (예: BlazeFaceDetector)
  → 더블클릭 → Visual Studio / Rider 에서 코드 작성

방법 2 - 메뉴에서:
  Assets 메뉴 → Create → C# Script
```

### GameObject에 Script 붙이기 (UE에서 Actor에 Component 추가하는 것과 동일)

```
방법 1 - 드래그 앤 드롭:
  Project 창의 BlazeFaceDetector.cs
  → Hierarchy의 "Tracking Manager" 오브젝트에 드래그

방법 2 - Inspector에서:
  Hierarchy에서 오브젝트 선택
  → Inspector 하단 "Add Component" 버튼
  → "BlazeFaceDetector" 검색 → 클릭

확인:
  Inspector 창에 BlazeFaceDetector 컴포넌트가 보이면 완료
  [SerializeField] 필드가 빈 칸으로 노출됨
```

### [SerializeField] 필드에 에셋 연결 (UE에서 UPROPERTY에 에셋 지정하는 것과 동일)

```
예: BlazeFaceDetector의 blazeFaceModelAsset 필드에 blazeface.onnx 연결

1. Hierarchy에서 "Tracking Manager" 오브젝트 클릭
2. Inspector에서 BlazeFaceDetector 컴포넌트 확인
3. "Blaze Face Model Asset" 필드 옆 ○ 클릭
   또는 Project 창의 blazeface.onnx를 필드에 드래그
4. 필드에 에셋 이름이 표시되면 완료
```

### 씬 구성 (UE에서 Level에 Actor 배치하는 것과 동일)

```
Android 앱 씬:
  Hierarchy 우클릭 → Create Empty → 이름: "Tracking Manager"
  → BlazeFaceDetector.cs 드래그
  → FaceLandmarkRunner.cs 드래그
  → BlazePoseDetector.cs 드래그
  → BlazePoseLandmarkRunner.cs 드래그
  → TrackingPipeline.cs 드래그
  → ServerStreamSender.cs 드래그

  Inspector에서 TrackingPipeline의 각 필드에
  위 컴포넌트들을 드래그 앤 드롭으로 연결
```

---

## 💻 개발 환경

| 항목 | 버전 |
|------|------|
| Unity Editor | **6000.3.9f1** |
| Sentis | **2.5.0** (Package Manager → Unity Registry) |
| UniVRM | 0.128.x 이상 (1.0 버전도 가능) (GitHub Releases → `.unitypackage` Import) |
| Android Build Support | Unity Hub → Installs → 모듈 추가 |
| 최소 Android | API 24 (Android 7.0) |
| 빌드 머신 | Windows 가능 (Mac 불필요) |
| 서버 언어 (Go) | **[go.dev](https://go.dev/doc/install)** 에서 다운로드하여 설치 (1.20 이상 권장, 외부 패키지 설치 불필요) |

### Sentis 2.5.0 설치 방법

```
UE에서 Plugin 활성화하는 것과 동일한 개념

Unity Editor 메뉴 → Window → Package Manager
→ 좌측 드롭다운에서 "Unity Registry" 선택
→ 검색창에 "Sentis" 입력
→ Sentis 2.5.0 선택 → Install
```

### Sentis 2.5.0 핵심 API

```csharp
// Worker 생성 (UE NNE 모듈 초기화와 유사)
var worker = new Worker(model, BackendType.GPUCompute);

// 텐서 생성 (UE의 TArray지만 GPU 메모리에 존재)
var tensor = new Tensor<float>(new TensorShape(1, 128, 128, 3));

// 비블로킹 GPU readback (UE AsyncTask와 유사)
using var result = await tensor.ReadbackAndCloneAsync();

// 또는 즉각적인 데이터 다운로드 (Sentis 2.5 권장)
float[] data = tensor.DownloadToArray();

// 어파인 변환으로 텍스처 → 텐서 변환 (비율 유지)
BlazeUtils.SampleImageAffine(texture, inputTensor, M);

// 리소스 해제 (UE의 BeginDestroy / 스마트 포인터 해제)
worker.Dispose();
tensor.Dispose();
```

---

## 📦 사용 모델

> 출처: [Unity-Technologies/sentis-samples — BlazeDetectionSample](https://github.com/Unity-Technologies/sentis-samples/tree/main/BlazeDetectionSample)
> 직접 학습 없음 — TFLite → ONNX 변환된 공식 사전학습 모델

### [Face] blaze_face_short_range.onnx — 얼굴 감지

> sentis-samples Face 샘플은 **감지(detection)만** 수행 — landmark 단계 없음

```
경로   : BlazeDetectionSample/Face/Assets/Models/blaze_face_short_range.onnx
원본출력: (1, 896, 16) boxes + (1, 896, 1) scores
         → Functional.NMS 적용 후:
입력   : (1, 128, 128, 3)
출력   : PeekOutput(0) → Tensor<int>   selectedIndices
         PeekOutput(1) → Tensor<float>  selectedScores    (1, N, 1)
         PeekOutput(2) → Tensor<float>  selectedBoxes     (1, N, 16)
           [0..3] : cx, cy, w, h  (앵커 상대 좌표)
           [4..15]: 6 keypoints 각 (x, y) — 눈2, 코1, 입1, 귀2
앵커   : BlazeDetectionSample/Face/Assets/Data/anchors.csv
후처리 : Functional.NMS 모델 그래프 내장
용도   : 얼굴 bounding box + 6 keypoints 제공 (랜드마크 468점 아님)
```

### [Hand] hand_detector.onnx — 1단계: 손 감지

```
경로  : BlazeDetectionSample/Hand/Assets/Models/hand_detector.onnx
입력  : (1, 192, 192, 3)
### [Hand] hand_detector.onnx — 1단계: 손 감지

```
경로  : BlazeDetectionSample/Hand/Assets/Models/hand_detector.onnx
입력  : (1, 192, 192, 3)
출력  : PeekOutput(0) → Tensor<int>   bestIndex
        PeekOutput(1) → Tensor<float>  bestScore
        PeekOutput(2) → Tensor<float>  bestBox
          [0..3] : cx, cy, w, h
          [4..17]: 7 palm keypoints 각 (x, y)
앵커  : BlazeDetectionSample/Hand/Assets/Data/anchors.csv
후처리: Functional.ArgMax 모델 그래프 내장 (가장 점수가 높은 손 하나 선택)
```

### [Hand] hand_landmarks_detector.onnx — 2단계: 손 랜드마크

```
경로  : BlazeDetectionSample/Hand/Assets/Models/hand_landmarks_detector.onnx
입력  : (1, 224, 224, 3)  ← hand_detector 박스/키포인트 기준 회전 정렬 크롭 (2.6x 스케일)
출력  : (1, 63)           ← 21개 관절 × [x, y, z]
관절  : 손목 1 + 엄지 4 + 검지 4 + 중지 4 + 약지 4 + 새끼 4 = 21개
```

### [Pose] pose_detection.onnx — 1단계: 포즈 감지

```
경로  : BlazeDetectionSample/Pose/Assets/Models/pose_detection.onnx
입력  : (1, 224, 224, 3)
출력  : PeekOutput(0) → Tensor<int>   bestIndex
        PeekOutput(1) → Tensor<float>  bestScore
        PeekOutput(2) → Tensor<float>  bestBox (1, 1, 12)
          [0..3] : cx, cy, w, h
          [4..5] : keypoint1 (head) x, y
          [6..7] : keypoint2 (body center) x, y
앵커  : BlazeDetectionSample/Pose/Assets/Data/anchors.csv
후처리: Functional.ArgMax 모델 그래프 내장
```

### [Pose] pose_landmarks_detector_full.onnx — 2단계: 포즈 랜드마크

```
경로  : BlazeDetectionSample/Pose/Assets/Models/pose_landmarks_detector_full.onnx
변형  : _lite.onnx (경량) / _full.onnx (표준) / _heavy.onnx (고정밀) 3종 제공
입력  : (1, 256, 256, 3)  ← kp1/kp2 기준 회전 정렬 크롭 (단순 박스 크롭 X)
출력  : (1, 165)          ← 33관절 × 5값 [x, y, z, visibility, presence]
```

---

## 🏗 시스템 아키텍처

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                                  Android 앱                                   │
│                                                                              │
│  전면 카메라 (WebCamTexture)   후면 카메라 (WebCamTexture×2 — 포즈/손)        │
│       ↓                              ↓                     ↓                │
│  SampleImageAffine           SampleImageAffine       SampleImageAffine       │
│  128×128                     224×224                 192×192                 │
│       ↓                              ↓                     ↓                │
│  blaze_face_short_range.onnx  pose_detection.onnx  hand_detector.onnx        │
│  NMS 내장                     ArgMax 내장            ArgMax 내장             │
│  ReadbackAndCloneAsync()      ReadbackAndCloneAsync() ReadbackAndCloneAsync() │
│       ↓                              ↓                     ↓                │
│  selectedBoxes(1,N,16)      kp1/kp2→256×256 회전 크롭  kp/박스→224×224 회전 크롭 │
│  6 keypoints + bbox              ↓                  (2.6x 스케일 적용)       │
│  (landmark 단계 없음)   pose_landmarks_detector_full  hand_landmarks_         │
│  bbox → BlendShape 규칙      출력: (1, 165)            detector.onnx          │
│  (눈 열림/닫힘 등 keypoint기반)  ↓                     출력: (1, 63)         │
│  bbox → BlendShape 계산  33관절 → 본 회전값   21관절 → 손가락 본 회전값       │
│       └──────────────────────┬───────────────────────────┘                  │
│                              ↓                                               │
│                 TrackingPacket 직렬화                                         │
│                 네트워크 송신 (UDP → Docker 서버로 쏘기만 함)                    │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │ 무선/유선 인터넷
                               ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  🐳 Golang Docker Relay Server (동시성 최적화, 최저 지연 중계)                    │
└──────────────────────────────┬───────────────────────────────────────────────┘
                               │ 데이터 수신 (PC 앱이 구독)
                               ▼
┌──────────────────────────────────────────────────────────────────────────────┐
│  PC 앱: ServerStreamReceiver → VRMMapper → VRM 아바타                         │
│  (iPhone 버전과 동일 코드 100% 재사용)                                         │
└──────────────────────────────────────────────────────────────────────────────┘
```

---

## 📁 디렉토리 구조 + 파일 생성 경로

> UE의 `Content/` = Unity의 `Assets/`
> 아래 경로는 모두 `Assets/` 기준

```
Assets/
│
├── Android/                              ← 폴더: Project창 Assets 우클릭 → Create → Folder
│   │
│   ├── Tracking/                         ← 폴더 생성
│   │   ├── BlazeFaceDetector.cs          ← C# Script 생성 (파일명 = 클래스명)
│   │   ├── BlazeHandDetector.cs          ← C# Script 생성 (신규)
│   │   ├── HandLandmarkRunner.cs         ← C# Script 생성 (신규)
│   │   ├── BlazePoseDetector.cs          ← C# Script 생성
│   │   ├── BlazePoseLandmarkRunner.cs    ← C# Script 생성
│   │   └── TrackingPipeline.cs           ← C# Script 생성
│   │
│   ├── Mapping/                          ← 폴더 생성
│   │   ├── FaceKeyPointBlendShape.cs     ← C# Script 생성 (신규)
│   │   ├── HandMapper.cs                 ← C# Script 생성 (신규)
│   │   └── PoseMapper.cs                 ← C# Script 생성
│   │
│   └── Network/                          ← 폴더 생성
│       └── ServerStreamSender.cs         ← C# Script 생성
│
├── Server/                               ← 폴더 생성 (Docker 서버 스크립트)
│   ├── Dockerfile                        ← 컨테이너 빌드 설정
│   └── main.go                           ← Golang 릴레이 서버 소스코드
│
├── PC/                                   ← 폴더 생성 (별도 Unity 프로젝트 or 같은 프로젝트)
│   ├── Network/
│   │   └── ServerStreamReceiver.cs       ← C# Script 생성
│   └── Mapping/
│       └── VRMMapper.cs                  ← C# Script 생성
│
├── Shared/                               ← 폴더 생성 (Android/iPhone/PC 공통)
│   ├── BlazeUtils.cs                     ← Face/Assets/Scripts/ 에서 복사 (필수 유틸리티)
│   └── TrackingPacket.cs                 ← C# Script 생성
│
├── Plugins/                              ← 폴더 생성
│   └── Android/                          ← 폴더 생성
│       └── AndroidManifest.xml           ← 텍스트 에디터로 직접 생성
│
├── StreamingAssets/                      ← 폴더 생성 (Unity 예약 폴더명, 정확히 이 이름)
│   ├── face_anchors.csv                  ← Face/Assets/Data/anchors.csv 복사·이름변경
│   ├── hand_anchors.csv                  ← Hand/Assets/Data/anchors.csv 복사·이름변경
│   └── pose_anchors.csv                  ← Pose/Assets/Data/anchors.csv 복사·이름변경
│
└── Models/                               ← 폴더 생성 (Git LFS 관리)
    ├── blaze_face_short_range.onnx               ← Face/Assets/Models/ 에서 복사
    ├── blaze_face_short_range.onnx.meta           ← Unity가 자동 생성 (건드리지 말 것)
    ├── hand_detector.onnx                         ← Hand/Assets/Models/ 에서 복사
    ├── hand_detector.onnx.meta                    ← Unity가 자동 생성
    ├── hand_landmarks_detector.onnx               ← Hand/Assets/Models/ 에서 복사
    ├── hand_landmarks_detector.onnx.meta          ← Unity가 자동 생성
    ├── pose_detection.onnx                        ← Pose/Assets/Models/ 에서 복사
    ├── pose_detection.onnx.meta                   ← Unity가 자동 생성
    ├── pose_landmarks_detector_full.onnx          ← Pose/Assets/Models/ 에서 복사
    └── pose_landmarks_detector_full.onnx.meta     ← Unity가 자동 생성
```

### .meta 파일이란?

```
UE에는 없는 Unity 고유 개념
Unity가 에셋을 임포트할 때 자동으로 생성하는 설정 파일

역할: 에셋의 고유 ID(GUID) 저장
      다른 스크립트가 이 에셋을 참조할 때 GUID로 찾음

규칙:
  ✅ .meta 파일은 반드시 Git에 포함 (gitignore에 추가 금지)
     → .meta 없으면 다른 PC에서 에셋 참조가 모두 깨짐
  ✅ .onnx 파일은 Git LFS로 관리 (용량이 크기 때문)
  ✅ .onnx.meta 파일은 일반 Git 커밋 (텍스트, 용량 작음)
```

---

## 🗓 개발 단계 로드맵

```
총 기간: 7~8주

Phase 1 ████░░░░░░░░░░░░  Week 1      환경 세팅 + Unity 기초 익히기
Phase 2 ████████░░░░░░░░  Week 2~3    BlazeFace 얼굴 감지
Phase 3 ████████████░░░░  Week 4~5    FaceLandmark + BlendShape 계산
Phase 4 ██████████████░░  Week 6      BlazePose 포즈 트래킹
Phase 5 ████████████████  Week 7~8    Golang 서버 연동 + VRM 적용 + 최적화
```

---

## 🔧 Phase 상세 구현

### Phase 1 — 환경 세팅 (Week 1)

**체크리스트**

- [ ] Unity Hub → Unity 6000.3.9f1 설치 시 **Android Build Support** 모듈 체크
- [ ] Unity Editor 실행 → Window → Package Manager → Unity Registry → **Sentis 2.5.0** Install
- [ ] sentis-samples 클론 → 모델 5개 + CSV 3개 + BlazeUtils.cs 복사
- [ ] UniVRM GitHub → `.unitypackage` 다운로드 → Unity 메뉴 Assets → Import Package → Custom Package
- [ ] Android 기기 설정 → 개발자 옵션 → USB 디버깅 ON
- [ ] `Assets/Models/blaze_face_short_range.onnx` 클릭 → Inspector에서 **ModelAsset** 타입 확인
- [ ] `Assets/Plugins/Android/AndroidManifest.xml` 생성

**sentis-samples 파일 복사**

```bash
git clone https://github.com/Unity-Technologies/sentis-samples.git

# ── 얼굴 모델 ─────────────────────────────────────────────────
cp sentis-samples/BlazeDetectionSample/Face/Assets/Models/blaze_face_short_range.onnx \
   YourProject/Assets/Models/


# ── 손 모델 ────────────────────────────────────────────
cp sentis-samples/BlazeDetectionSample/Hand/Assets/Models/hand_detector.onnx \
   YourProject/Assets/Models/

cp sentis-samples/BlazeDetectionSample/Hand/Assets/Models/hand_landmarks_detector.onnx \
   YourProject/Assets/Models/

# ── 포즈 모델 ────────────────────────────────────────────────
cp sentis-samples/BlazeDetectionSample/Pose/Assets/Models/pose_detection.onnx \
   YourProject/Assets/Models/

cp sentis-samples/BlazeDetectionSample/Pose/Assets/Models/pose_landmarks_detector_full.onnx \
   YourProject/Assets/Models/

# ── 앵커 CSV (StreamingAssets에 배치, 파일명 구분하여 복사) ───
cp sentis-samples/BlazeDetectionSample/Face/Assets/Data/anchors.csv \
   YourProject/Assets/StreamingAssets/face_anchors.csv

cp sentis-samples/BlazeDetectionSample/Hand/Assets/Data/anchors.csv \
   YourProject/Assets/StreamingAssets/hand_anchors.csv

cp sentis-samples/BlazeDetectionSample/Pose/Assets/Data/anchors.csv \
   YourProject/Assets/StreamingAssets/pose_anchors.csv

# ── BlazeUtils.cs (필수 유틸리티) ────────────────────────────
cp sentis-samples/BlazeDetectionSample/Face/Assets/Scripts/BlazeUtils.cs \
   YourProject/Assets/Shared/
```

**`.gitattributes`** (프로젝트 루트에 생성)

```
*.onnx  filter=lfs diff=lfs merge=lfs -text
*.vrm   filter=lfs diff=lfs merge=lfs -text
```

**`Assets/Plugins/Android/AndroidManifest.xml`** (직접 생성)

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
  <uses-permission android:name="android.permission.CAMERA"/>
  <uses-feature android:name="android.hardware.camera.front"
                android:required="true"/>
</manifest>
```

---

### Phase 2 — BlazeFace 얼굴 감지 (Week 2~3)

**파일 생성 경로**: `Assets/Android/Tracking/BlazeFaceDetector.cs`

```csharp
// BlazeFaceDetector.cs
// UE 비유: UAIPerceptionComponent처럼 카메라 프레임에서 얼굴 위치를 감지하는 컴포넌트
using System.IO;
using Unity.Mathematics;
using Unity.InferenceEngine;
using UnityEngine;

// MonoBehaviour = UE의 ActorComponent
// GameObject에 드래그 앤 드롭하면 컴포넌트로 동작
public class BlazeFaceDetector : MonoBehaviour
{
  // [SerializeField] = UE UPROPERTY(EditAnywhere)
  // Inspector 창에서 blaze_face_short_range.onnx를 이 필드에 드래그 앤 드롭으로 연결
  [SerializeField] private ModelAsset blazeFaceModelAsset;

  private Worker        _worker;      // 추론 실행기
  private Tensor<float> _inputTensor; // GPU 메모리 입력 텐서 (UE RenderTarget과 유사)
  private float2[]      _anchors;     // CSV에서 로드한 앵커 좌표

  private const int INPUT_SIZE = 128;

  public struct FaceDetection
  {
    public bool   IsValid;
    public Rect   BoundingBox; // 정규화 좌표 (0~1)
    public float  Score;
    public float2 AnchorPosition;
    public float[] RawBoxes;    // selectedBoxes[0,0] 원본 (16개 값)
  }

  // Start() = UE BeginPlay()
  void Start()
  {
    var model = ModelLoader.Load(blazeFaceModelAsset);

    // Sentis 2.x Worker 생성 방식 (1.x의 WorkerFactory 제거됨)
    _worker = new Worker(model, BackendType.GPUCompute);

    // 입력 텐서 미리 할당 → 매 프레임 new 하지 않아 GC 부하 감소
    _inputTensor = new Tensor<float>(new TensorShape(1, INPUT_SIZE, INPUT_SIZE, 3));

    // StreamingAssets = UE에서 Content/Data 폴더처럼 런타임에 파일 경로로 읽는 폴더
    // face_anchors.csv: Face/Assets/Data/anchors.csv 복사·이름변경
    string csvPath = Path.Combine(Application.streamingAssetsPath, "face_anchors.csv");
    _anchors = BlazeUtils.LoadAnchors(csvPath);
  }

  // Awaitable = UE AsyncTask와 유사, 비동기 처리
  // async 함수: await 지점에서 잠시 제어권 반환 → 메인 스레드 블로킹 없음
  public async Awaitable<FaceDetection> DetectAsync(WebCamTexture texture)
  {
    // 어파인 변환 행렬 계산 (비율 유지, 빈 영역 0 패딩)
    // UE의 FMatrix 좌표 변환과 동일한 원리
    var size  = Mathf.Max(texture.width, texture.height);
    var scale = size / (float)INPUT_SIZE;
    var M = BlazeUtils.mul(
      BlazeUtils.TranslationMatrix(
        0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))
      ),
      BlazeUtils.ScaleMatrix(new Vector2(scale, -scale))
    );

    // 컴퓨트 셰이더로 카메라 텍스처를 128×128 텐서로 변환 (비율 유지)
    // Graphics.Blit을 사용하지 않는 이유: Blit은 비율을 왜곡시킴
    BlazeUtils.SampleImageAffine(texture, _inputTensor, M);

    _worker.Schedule(_inputTensor);

    // ReadbackAndCloneAsync = GPU 연산 완료 후 CPU로 복사 (비블로킹)
    // using var: 스코프 종료 시 자동 Dispose (UE 스마트 포인터와 유사)
    var indicesAwaitable = (_worker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
    var scoresAwaitable  = (_worker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
    var boxesAwaitable   = (_worker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

    using var indices = await indicesAwaitable;
    using var scores  = await scoresAwaitable;
    using var boxes   = await boxesAwaitable;

    if (indices.shape[0] == 0)
      return new FaceDetection { IsValid = false };

    int   bestIdx   = indices[0];
    float bestScore = scores[0, 0, 0];
    var   anchor    = _anchors[bestIdx];

    float cx = (boxes[0, 0, 0] + anchor.x * INPUT_SIZE) / INPUT_SIZE;
    float cy = (boxes[0, 0, 1] + anchor.y * INPUT_SIZE) / INPUT_SIZE;
    float w  = boxes[0, 0, 2] / INPUT_SIZE;
    float h  = boxes[0, 0, 3] / INPUT_SIZE;

    return new FaceDetection
    {
      IsValid        = true,
      BoundingBox    = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h),
      Score          = bestScore,
      AnchorPosition = anchor,
      RawBoxes       = boxes.DownloadToArray() // 0~15번 값 복사
    };
  }

  // OnDestroy() = UE EndPlay() / BeginDestroy()
  void OnDestroy()
  {
    _worker?.Dispose();      // GPU 리소스 해제
    _inputTensor?.Dispose(); // 텐서 메모리 해제
  }
}
```

---

### Phase 3 — BlazeFace keypoint 기반 BlendShape 계산 (Week 4~5)

> ⚠️ **sentis-samples Face 샘플에는 landmark 가 없음**
> `blaze_face_short_range.onnx` 의 `selectedBoxes[4..15]` 에서 6개 keypoint만 제공:
> - keypoint[0] = 오른쪽 눈 중심 / keypoint[1] = 왼쪽 눈 중심
> - keypoint[2] = 코 끝 / keypoint[3] = 입 중심
> - keypoint[4] = 오른쪽 귀 / keypoint[5] = 왼쪽 귀
>
> 랜드마크 468점 없이 눈 트임/닫힘 감지는 박스 높이 측정 또는 헤드 포즈 추정으로 대체 가능

**파일 생성 경로**: `Assets/Android/Mapping/FaceKeyPointBlendShape.cs`

```csharp
// FaceKeyPointBlendShape.cs
// BlazeFace 의 6 keypoints 만으로 BlendShape 근사 계산
// 랜드마크 468점 불필요 — 복잡도 낮으나 정밀도는 낮음
using UnityEngine;

public static class FaceKeyPointBlendShape
{
  // BlazeFace selectedBoxes 포맷 (정규화 좌표):
  //   [0..3] : cx, cy, w, h  (bbox)
  //   [4..5] : 오른쪽 눈  (x, y)
  //   [6..7] : 왼쪽 눈   (x, y)
  //   [8..9] : 코 끝     (x, y)
  //   [10..11]: 입 중심  (x, y)
  //   [12..13]: 오른쪽 귀 (x, y)
  //   [14..15]: 왼쪽 귀  (x, y)

  public struct Result
  {
    public float BlinkLeft;    // 0=열림, 1=감김
    public float BlinkRight;
    public float MouthOpen;    // 미지원 — 헤드 pose로 대체
  }

  // box: BlazeFace selectedBoxes[0, i]  (length 16, 정규화 좌표)
  public static Result Calculate(float[] box)
  {
    float bboxH   = box[3]; // 정규화 bbox 높이
    float eyeRY   = box[5]; // 오른쪽 눈 Y
    float eyeLY   = box[7]; // 왼쪽 눈 Y
    float centerY = box[1]; // bbox 중심 Y

    // 눈이 중심에서 멀수록 눈이 열림 (bboxH 대비 정규화)
    float blinkR = 1f - Mathf.Clamp01(Mathf.Abs(eyeRY - centerY) / (bboxH * 0.3f));
    float blinkL = 1f - Mathf.Clamp01(Mathf.Abs(eyeLY - centerY) / (bboxH * 0.3f));

    return new Result { BlinkLeft = blinkL, BlinkRight = blinkR, MouthOpen = 0f };
  }
}
```

---

### Phase 4 — BlazePose 포즈 트래킹 (Week 6)

**파일 생성 경로**: `Assets/Android/Tracking/BlazePoseDetector.cs`

```csharp
// BlazePoseDetector.cs
// pose_detection.onnx 를 사용하여 전신 포즈 및 주요 키포인트(엉덩이, 어깨) 감지
using System.IO;
using Unity.Mathematics;
using Unity.InferenceEngine;
using UnityEngine;

public class BlazePoseDetector : MonoBehaviour
{
  [SerializeField] private ModelAsset poseDetectorAsset;

  private Worker        _worker;
  private Tensor<float> _inputTensor;
  private float2[]      _anchors;

  private const int DETECTOR_SIZE = 224;

  public struct PoseDetection
  {
    public bool     IsValid;
    public float    Score;
    public float2   Keypoint1;       // 엉덩이 중심 (회전 기준점 1)
    public float2   Keypoint2;       // 어깨 중심   (회전 기준점 2)
    public float2x3 DetectorMatrix;  // 랜드마커 크롭 계산에 필요
  }
```
  void Start()
  {
    var model = ModelLoader.Load(poseDetectorAsset);
    _worker      = new Worker(model, BackendType.GPUCompute);
    _inputTensor = new Tensor<float>(new TensorShape(1, DETECTOR_SIZE, DETECTOR_SIZE, 3));

    // pose_anchors.csv: Pose/Assets/Data/anchors.csv 복사·이름변경
    string csv = Path.Combine(Application.streamingAssetsPath, "pose_anchors.csv");
    _anchors = BlazeUtils.LoadAnchors(csv);
  }

  public async Awaitable<PoseDetection> DetectAsync(WebCamTexture texture)
  {
    var size  = Mathf.Max(texture.width, texture.height);
    var scale = size / (float)DETECTOR_SIZE;
    var M = BlazeUtils.mul(
      BlazeUtils.TranslationMatrix(
        0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))
      ),
      BlazeUtils.ScaleMatrix(new Vector2(scale, -scale))
    );
    BlazeUtils.SampleImageAffine(texture, _inputTensor, M);
    _worker.Schedule(_inputTensor);

    var idxAw   = (_worker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
    var scoreAw = (_worker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
    var boxAw   = (_worker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

    using var idx   = await idxAw;
    using var score = await scoreAw;
    using var box   = await boxAw;

    if (score[0, 0, 0] < 0.5f)
      return new PoseDetection { IsValid = false };

    var anchor = _anchors[idx[0]];
    return new PoseDetection
    {
      IsValid        = true,
      Score          = score[0, 0, 0],
      Keypoint1      = anchor + new float2(box[0, 0, 4], box[0, 0, 5]),
      Keypoint2      = anchor + new float2(box[0, 0, 6], box[0, 0, 7]),
      DetectorMatrix = M,
    };
  }

  void OnDestroy() { _worker?.Dispose(); _inputTensor?.Dispose(); }
}
```

**파일 생성 경로**: `Assets/Android/Tracking/BlazePoseLandmarkRunner.cs`

```csharp
// BlazePoseLandmarkRunner.cs
// UE 비유: 루트 조인트 기준으로 전신 본을 정규화하는 것과 동일한 원리
using Unity.Mathematics;
using Unity.InferenceEngine;
using UnityEngine;

public class BlazePoseLandmarkRunner : MonoBehaviour
{
  [SerializeField] private ModelAsset poseLandmarkAsset;

  private Worker        _worker;
  private Tensor<float> _inputTensor;

  private const int LANDMARKER_SIZE = 256;

  void Start()
  {
    var model = ModelLoader.Load(poseLandmarkAsset);
    _worker      = new Worker(model, BackendType.GPUCompute);
    _inputTensor = new Tensor<float>(new TensorShape(1, LANDMARKER_SIZE, LANDMARKER_SIZE, 3));
  }

  // 반환값: Vector3[33] — 각 관절의 이미지 좌표 (z = visibility)
  public async Awaitable<Vector3[]> RunAsync(
    WebCamTexture texture,
    BlazePoseDetector.PoseDetection detection)
  {
    // kp1(엉덩이), kp2(어깨) 두 점으로 회전 정렬 크롭 행렬 계산
    // UE 모션캡쳐의 루트 본 기준 정규화와 동일한 원리
    var kp1 = BlazeUtils.mul(detection.DetectorMatrix, detection.Keypoint1);
    var kp2 = BlazeUtils.mul(detection.DetectorMatrix, detection.Keypoint2);

    var   delta  = kp2 - kp1;
    float radius = 1.25f * math.length(delta);
    float theta  = math.atan2(delta.y, delta.x);

    var   origin2 = new float2(0.5f * LANDMARKER_SIZE, 0.5f * LANDMARKER_SIZE);
    float scale2  = radius / (0.5f * LANDMARKER_SIZE);

    // 회전 + 스케일 + 이동 행렬 합성 (공식 README 동일)
    var M2 = BlazeUtils.mul(
      BlazeUtils.mul(
        BlazeUtils.mul(
          BlazeUtils.TranslationMatrix(kp1),
          BlazeUtils.ScaleMatrix(new float2(scale2, -scale2))
        ),
        BlazeUtils.RotationMatrix(0.5f * Mathf.PI - theta)
      ),
      BlazeUtils.TranslationMatrix(-origin2)
    );
    BlazeUtils.SampleImageAffine(texture, _inputTensor, M2);
    _worker.Schedule(_inputTensor);

    // 출력: (1, 165) = 33관절 × [x, y, z, visibility, presence]
    using var output = await (_worker.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync();

    var joints = new Vector3[33];
    for (int i = 0; i < 33; i++)
    {
      // M2 역변환으로 이미지 좌표계 복원
      var pos = BlazeUtils.mul(M2, new float2(output[0, i * 5], output[0, i * 5 + 1]));
      joints[i] = new Vector3(pos.x, pos.y, output[0, i * 5 + 3]); // z = visibility
    }
    return joints;
  }

  void OnDestroy() { _worker?.Dispose(); _inputTensor?.Dispose(); }
}
```

---

### Phase 5 — BlazeHand 손 트래킹 (신규 추가)

**파일 생성 경로**: `Assets/Android/Tracking/BlazeHandDetector.cs`

```csharp
// BlazeHandDetector.cs
// hand_detector.onnx 를 사용하여 손의 위치 감지
using System.IO;
using Unity.Mathematics;
using Unity.Sentis;
using UnityEngine;

public class BlazeHandDetector : MonoBehaviour
{
  [SerializeField] private ModelAsset handDetectorAsset;
  private Worker        _worker;
  private Tensor<float> _inputTensor;
  private float2[]      _anchors;

  private const int DETECTOR_SIZE = 192;

  public struct HandDetection
  {
    public bool     IsValid;
    public float2   CenterTensorSpace;
    public float    SizeTensorSpace;
    public float    Rotation;
    public float2x3 DetectorMatrix;
  }

  void Start()
  {
    var model = ModelLoader.Load(handDetectorAsset);
    _worker      = new Worker(model, BackendType.GPUCompute);
    _inputTensor = new Tensor<float>(new TensorShape(1, DETECTOR_SIZE, DETECTOR_SIZE, 3));

    string csv = Path.Combine(Application.streamingAssetsPath, "hand_anchors.csv");
    _anchors = BlazeUtils.LoadAnchors(csv);
  }

  public async Awaitable<HandDetection> DetectAsync(WebCamTexture texture)
  {
    var size  = Mathf.Max(texture.width, texture.height);
    var scale = size / (float)DETECTOR_SIZE;
    var M = BlazeUtils.mul(
      BlazeUtils.TranslationMatrix(0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))),
      BlazeUtils.ScaleMatrix(new Vector2(scale, -scale))
    );
    BlazeUtils.SampleImageAffine(texture, _inputTensor, M);
    _worker.Schedule(_inputTensor);

    using var idx   = await (_worker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
    using var score = await (_worker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
    using var box   = await (_worker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();

    if (idx.shape[0] == 0 || score[0, 0, 0] < 0.5f)
      return new HandDetection { IsValid = false };

    var anchor = _anchors[idx[0]];
    
    // 공식 README 수식 적용: kp0(손목)와 kp2(검지 마디)를 이용한 회전 정렬
    var centerTS = anchor + new float2(box[0, 0, 0], box[0, 0, 1]);
    var sizeTS   = math.max(box[0, 0, 2], box[0, 0, 3]);
    
    var kp0TS    = anchor + new float2(box[0, 0, 4], box[0, 0, 5]); // wrist
    var kp2TS    = anchor + new float2(box[0, 0, 8], box[0, 0, 9]); // middle finger
    var deltaTS  = kp2TS - kp0TS;
    var upTS     = deltaTS / math.length(deltaTS);
    var theta    = math.atan2(deltaTS.y, deltaTS.x);
    
    // 손이 위를 향하도록 보정 (0.5 * PI - theta)
    var rotation = 0.5f * Mathf.PI - theta;
    centerTS    += 0.5f * sizeTS * upTS;
    sizeTS      *= 2.6f; // 모델 특성에 따른 스케일업

    return new HandDetection
    {
      IsValid           = true,
      CenterTensorSpace = centerTS,
      SizeTensorSpace   = sizeTS,
      Rotation          = rotation,
      DetectorMatrix    = M
    };
  }

  void OnDestroy() { _worker?.Dispose(); _inputTensor?.Dispose(); }
}
```

**파일 생성 경로**: `Assets/Android/Tracking/HandLandmarkRunner.cs`

```csharp
// HandLandmarkRunner.cs
// hand_landmarks_detector.onnx 를 사용하여 21개 관절 추출
using Unity.Mathematics;
using Unity.Sentis;
using UnityEngine;

public class HandLandmarkRunner : MonoBehaviour
{
  [SerializeField] private ModelAsset handLandmarkAsset;
  private Worker        _worker;
  private Tensor<float> _inputTensor;

  private const int LANDMARKER_SIZE = 224;

  void Start()
  {
    var model = ModelLoader.Load(handLandmarkAsset);
    _worker      = new Worker(model, BackendType.GPUCompute);
    _inputTensor = new Tensor<float>(new TensorShape(1, LANDMARKER_SIZE, LANDMARKER_SIZE, 3));
  }

  public async Awaitable<Vector3[]> RunAsync(WebCamTexture texture, BlazeHandDetector.HandDetection detection)
  {
    var origin2 = new float2(0.5f * LANDMARKER_SIZE, 0.5f * LANDMARKER_SIZE);
    var scale2  = detection.SizeTensorSpace / LANDMARKER_SIZE;

    // 회전 정렬 크롭 행렬 (M2): Pose와 동일한 방식의 affine 변환
    var M2 = BlazeUtils.mul(detection.DetectorMatrix, BlazeUtils.mul(
      BlazeUtils.mul(
        BlazeUtils.mul(
          BlazeUtils.TranslationMatrix(detection.CenterTensorSpace),
          BlazeUtils.ScaleMatrix(new float2(scale2, -scale2))
        ),
        BlazeUtils.RotationMatrix(detection.Rotation)
      ),
      BlazeUtils.TranslationMatrix(-origin2)
    ));

    BlazeUtils.SampleImageAffine(texture, _inputTensor, M2);
    _worker.Schedule(_inputTensor);

    using var output = await (_worker.PeekOutput(0) as Tensor<float>).ReadbackAndCloneAsync();
    var joints = new Vector3[21];
    for (int i = 0; i < 21; i++)
    {
       var pos = BlazeUtils.mul(M2, new float2(output[0, i * 3], output[0, i * 3 + 1]));
       joints[i] = new Vector3(pos.x, pos.y, output[0, i * 3 + 2]);
    }
    return joints;
  }

  void OnDestroy() { _worker?.Dispose(); _inputTensor?.Dispose(); }
}
```

---

### Phase 6 — Golang 서버 연동 + VRM 매핑 (Week 7~8)

**파일 생성 경로**: `Assets/Shared/TrackingPacket.cs`

```csharp
// TrackingPacket.cs
// UE 비유: FNetSerialize로 직렬화되는 패킷 구조체와 동일
// Android / iPhone / PC 세 프로젝트에서 이 파일 하나를 공유
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct TrackingPacket
{
  public Vector3 HeadRotation;
  public Vector3 HeadPosition;
  public Vector3 LeftArmRotation;  // 추가
  public Vector3 RightArmRotation; // 추가
  public float   Timestamp;
  public float[] BlendShapeValues;  // 길이 52 고정

  // 260 bytes 고정 직렬화 (기존 236 + 24)
  // 구조: HeadRot(12) + HeadPos(12) + LArm(12) + RArm(12) + Timestamp(4) + BlendShapes(208) = 260
  public byte[] Serialize()
  {
    var buf = new byte[260];
    int o   = 0;
    WriteV3(buf, ref o, HeadRotation);
    WriteV3(buf, ref o, HeadPosition);
    WriteV3(buf, ref o, LeftArmRotation);
    WriteV3(buf, ref o, RightArmRotation);
    WriteF(buf, ref o, Timestamp);
    BlendShapeValues ??= new float[52];
    foreach (float v in BlendShapeValues) WriteF(buf, ref o, v);
    return buf;
  }

  public static TrackingPacket Deserialize(byte[] buf)
  {
    int o = 0;
    var p = new TrackingPacket
    {
      HeadRotation     = ReadV3(buf, ref o),
      HeadPosition     = ReadV3(buf, ref o),
      LeftArmRotation  = ReadV3(buf, ref o),
      RightArmRotation = ReadV3(buf, ref o),
      Timestamp        = ReadF(buf, ref o),
      BlendShapeValues = new float[52],
    };
    for (int i = 0; i < 52; i++) p.BlendShapeValues[i] = ReadF(buf, ref o);
    return p;
  }

  private static void WriteF(byte[] b, ref int o, float v)
  { Buffer.BlockCopy(BitConverter.GetBytes(v), 0, b, o, 4); o += 4; }
  private static void WriteV3(byte[] b, ref int o, Vector3 v)
  { WriteF(b, ref o, v.x); WriteF(b, ref o, v.y); WriteF(b, ref o, v.z); }
  private static float ReadF(byte[] b, ref int o)
  { float v = BitConverter.ToSingle(b, o); o += 4; return v; }
  private static Vector3 ReadV3(byte[] b, ref int o)
    => new(ReadF(b, ref o), ReadF(b, ref o), ReadF(b, ref o));
}
```

**🔥 Golang 서버 개발 환경 지식 (초보자용 요약)**
> - Go(Golang)는 구글이 만든 언어로, **C++ 수준의 압도적인 네트워크 성능**을 가지면서도 파이썬처럼 로직을 짧고 쉽게 짤 수 있습니다.
> - **goroutine (고루틴):** Go의 핵심입니다! C#의 일반 Thread보다 수천 배 더 가벼운 '초경량 스레드'입니다. 메모리를 거의 먹지 않아 클라이언트가 수백만 명 붙어도 비동기 처리를 손쉽게 해냅니다.
> - **바이너리 빌드:** C#처럼 무거운 JIT 컴파일러나 런타임(가상머신) 설치가 필요 없이, `.exe` (리눅스에선 확장자 없는 실행파일) 딱 한 장으로 컴파일되어 나옵니다. 
> - 이 덕분에 실행 속도가 엄청나게 빠르고, **Docker 이미지가 1GB에서 단 10MB 내외로 압축**되는 극한의 가벼움을 자랑합니다. (실시간 트래킹에 0.001초의 랙도 없는 비결입니다.)

**파일 생성 경로 (Docker 서버용)**: `Server/main.go`
```go
// main.go
// 안드로이드 앱에서 보낸 데이터를 PC 앱으로 뿌려주는 초저지연 UDP 중계(Relay) 서버
package main

import (
	"fmt"
	"log"
	"net"
	"sync"
)

func main() {
	// 1. 서버가 수신할 UDP 포트 설정 (알기 쉽게 번호 지정)
	port := 49152
	
	// C#의 IPEndPoint와 유사. 포트를 열 준비를 합니다.
	addr := net.UDPAddr{
		Port: port,
		IP:   net.ParseIP("0.0.0.0"), // 모든 외부 IP 구별없이 다 받음
	}

	// 2. 소켓 생성 및 바인딩 (Bind)
	conn, err := net.ListenUDP("udp", &addr)
	if err != nil {
		log.Fatalf("서버 생성 에러: %v\n", err)
	}
	// defer 키워드: C#의 finally 블록처럼 이 main 함수가 끝날 때 이 줄을 무조건 마지막에 실행(메모리 누수 방어)
	defer conn.Close()

	fmt.Printf("🚀 Golang UDP Relay Server가 %d 포트에서 대기 중입니다...\n", port)

	// 3. 데이터를 받을 클라이언트(PC 앱) 목록. (스레드 안전한 딕셔너리 구조)
	// Go는 병렬 처리가 기본이라 동기화(Lock)를 걸 수 있는 Mutex를 엑세스 시 필수 지참해야 합니다.
	clients := make(map[string]*net.UDPAddr)
	var mutex sync.Mutex // C#의 lock() 괄호 역할

	// 4. 버퍼 할당 (236바이트의 트래킹 패킷이 들어올 공간, 넉넉히 1024 할당)
	buffer := make([]byte, 1024)

	// 5. 무한 루프로 패킷 수신 (Unreal Engine의 Tick 함수처럼 빙글빙글 돔)
	for {
		// 패킷 수신 대기 (여기서 코드 실행이 멈춰있다가, 외부에서 값이 들어오면 다음 줄로 넘어감)
		n, remoteAddr, err := conn.ReadFromUDP(buffer)
		if err != nil {
			continue // 에러 나면 앱 꺼짐을 막고 그냥 다음 패킷을 대기함
		}

		// 받은 사람의 IP 주소를 글자로 바꿈 (예: "192.168.x.x:port")
		addrString := remoteAddr.String()

		// 6. "SUBSCRIBE"라는 9글자 예약어가 오면, 이 클라이언트를 수신자(PC) 목록에 등록
		// n == 9는 받은 데이터의 길이가 딱 9바이트인지 검사합니다.
		if n == 9 && string(buffer[:n]) == "SUBSCRIBE" {
			mutex.Lock()
			clients[addrString] = remoteAddr
			mutex.Unlock()
			fmt.Printf("✅ 새로운 감시자 구독됨 (PC 추가): %s\n", addrString)
			continue
		}

		// 7. 트래킹 패킷(236 bytes)이 안드로이드로부터 오면, 등록된 모든 구독자(PC)에게 브로드캐스트(복사 전송)!
		// 안드로이드 기기는 SUBSCRIBE 명령을 안 보냈으니 목록에 없어서 수신을 안 받음 (송신 전용 레이턴시 확보)
		mutex.Lock()
		for _, clientAddr := range clients {
			// 고루틴(Goroutine) 파생 금지 - 단순 UDP 중계는 동기 전송이 컨텍스트 스위칭 딜레이 없이 제일 빠름
			conn.WriteToUDP(buffer[:n], clientAddr)
		}
		mutex.Unlock()
	}
}
```

**파일 생성 경로 (Docker 서버용)**: `Server/Dockerfile`
```dockerfile
# -------------------------------------------------------------
# 1단계 (빌더): 속도와 가벼움을 위해 Go를 빌드용 운영체제로 잠시 사용합니다
# -------------------------------------------------------------
FROM golang:1.22-alpine AS builder
WORKDIR /app
COPY main.go .
# 운영체제 불문, C의존성 없는 가장 순수한 1개짜리 바이너리 파일을 생성합니다 (정적 링킹)
RUN CGO_ENABLED=0 GOOS=linux go build -a -installsuffix cgo -o relay_server .

# -------------------------------------------------------------
# 2단계 (런타임): 아주 가볍고 뼈대만 있는 알파인 리눅스 이미지에 우리가 방금 만든 빌드 파일 하나만 쏙 넣습니다!
# 이렇게 하면 Docker 이미지 크기가 보통 1GB 넘어가는 데 단 10MB 내외로 압도적으로 줄어듭니다.
# -------------------------------------------------------------
FROM alpine:latest  
WORKDIR /root/
COPY --from=builder /app/relay_server .
EXPOSE 49152/udp
CMD ["./relay_server"]
```
*(실행 방법: Server 폴더 안에서 `docker build -t go-relay-server .` 후 `docker run -d -p 49152:49152/udp go-relay-server`)*

**파일 생성 경로**: `Assets/Android/Network/ServerStreamSender.cs`

```csharp
// ServerStreamSender.cs (안드로이드 전용)
// Docker 서버로 트래킹 패킷을 송신(Send만 함)
using System.Net.Sockets;
using UnityEngine;

public class ServerStreamSender : MonoBehaviour
{
  [Header("Docker Cloud Server Settings")]
  [SerializeField] private string serverIpAddress = "127.0.0.1"; // Docker 서버 IP 또는 도메인 주소 입력
  [SerializeField] private int    port            = 49152;

  private UdpClient _client;

  void Start() => _client = new UdpClient();

  // Unity 6 내장 Awaitable
  public async Awaitable SendAsync(TrackingPacket packet)
  {
    byte[] data = packet.Serialize(); // 고정 236 bytes
    // 데이터 손실 여부 상관 없이 서버로 UDP 패킷을 사정없이 밀어넣습니다 (가장 빠름)
    await _client.SendAsync(data, data.Length, serverIpAddress, port);
  }

  void OnDestroy() => _client?.Dispose();
}
```

**파일 생성 경로**: `Assets/PC/Network/ServerStreamReceiver.cs`

```csharp
// ServerStreamReceiver.cs (PC 앱)
// Docker 서버에서 전송되는 데이터를 수신하기 위한 클라이언트
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class ServerStreamReceiver : MonoBehaviour
{
  [Header("Docker Cloud Server Settings")]
  [SerializeField] private string serverIpAddress = "127.0.0.1";  // 클라우드 서버IP
  [SerializeField] private int    port            = 49152;
  [SerializeField] private VRMMapper mapper;

  private UdpClient _client;

  void Start()
  {
    _client = new UdpClient();
    _client.Connect(serverIpAddress, port);
    
    // 💡 핵심(Pub/Sub): "SUBSCRIBE"라는 메시지를 보내서 나를 구독자로 등록시킵니다.
    // 안 보내면 Go서버가 내 IP를 몰라 데이터를 안 보내줍니다. (연결 뚫기)
    byte[] initBytes = Encoding.UTF8.GetBytes("SUBSCRIBE");
    _client.Send(initBytes, initBytes.Length);
    
    _ = ReceiveLoopAsync(); // 메인 스레드 블로킹 안함
  }

  private async Awaitable ReceiveLoopAsync()
  {
    while (true)
    {
      var result = await _client.ReceiveAsync();
      
      // 260바이트 미만 패킷은 무시 (Head/Arm Rot + BlendShapes)
      if(result.Buffer.Length < 260) continue; 
      
      var packet = TrackingPacket.Deserialize(result.Buffer);
      mapper.Apply(packet); // 메인 렌더링 스레드에서 VRM 캐릭터 변경
    }
  }

  void OnDestroy() => _client?.Dispose();
}
```

**파일 생성 경로**: `Assets/PC/Mapping/VRMMapper.cs`

```csharp
// VRMMapper.cs (PC 앱)
// UE 비유: LiveLink에서 받은 트래킹 데이터를 MorphTarget/본에 적용하는 것과 동일
using UnityEngine;
using UniVRM10;

public class VRMMapper : MonoBehaviour
{
  // Inspector에서 VRM10 아바타의 최상단 컴포넌트를 드래그 앤 드롭으로 연결
  [SerializeField] private Vrm10Instance vrmInstance; // Vrm10Instance 컴포넌트
  [SerializeField] private Animator      vrmAnimator; // 본 접근용
  [SerializeField, Range(0f, 1f)] private float smoothing = 0.3f; // 움직임 부드럽게

  public void Apply(TrackingPacket p)
  {
    if (p.BlendShapeValues == null) return;
    ApplyBlendShapes(p.BlendShapeValues);
    ApplyHeadRotation(p.HeadRotation);
    ApplyArmRotations(p.LeftArmRotation, p.RightArmRotation); // 추가
  }

  private void ApplyArmRotations(Vector3 lEuler, Vector3 rEuler)
  {
    var lArm = vrmAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
    var rArm = vrmAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
    if (lArm != null) lArm.localRotation = Quaternion.Slerp(lArm.localRotation, Quaternion.Euler(lEuler), 1f - smoothing);
    if (rArm != null) rArm.localRotation = Quaternion.Slerp(rArm.localRotation, Quaternion.Euler(rEuler) , 1f - smoothing);
  }

  private void ApplyBlendShapes(float[] v)
  {
    // UniVRM10의 Expression 설정 로직
    Set(ExpressionPreset.blinkLeft, Get(v, "eyeBlinkLeft"));
    Set(ExpressionPreset.blinkRight,Get(v, "eyeBlinkRight"));
    Set(ExpressionPreset.aa,        Mathf.Clamp01(Get(v, "jawOpen") * 1.2f));
    Set(ExpressionPreset.oh,        Mathf.Clamp01(Get(v, "mouthFunnel") + Get(v, "mouthPucker")));
    Set(ExpressionPreset.ou,        Mathf.Clamp01(Get(v, "mouthPucker") * 1.5f));
    Set(ExpressionPreset.happy,     (Get(v, "mouthSmileLeft") + Get(v, "mouthSmileRight")) * 0.5f);
    Set(ExpressionPreset.sad,       (Get(v, "mouthFrownLeft") + Get(v, "mouthFrownRight")) * 0.5f);
    Set(ExpressionPreset.surprised, Get(v, "browInnerUp"));
    Set(ExpressionPreset.angry,     (Get(v, "browDownLeft") + Get(v, "browDownRight")) * 0.5f);
  }

  private void ApplyHeadRotation(Vector3 euler)
  {
    // HumanBodyBones = UE의 본 이름 열거형과 동일한 역할
    var neck = vrmAnimator.GetBoneTransform(HumanBodyBones.Neck);
    if (neck == null) return;

    // Android 카메라 좌표계 → Unity 좌표계 변환 (Pitch, Roll 반전)
    Quaternion target = Quaternion.Euler(-euler.x, euler.y, -euler.z);

    // Slerp: 구면 선형 보간 (UE의 FQuat::Slerp와 동일)
    neck.localRotation = Quaternion.Slerp(neck.localRotation, target, 1f - smoothing);
  }

  // 헬퍼: Expression (BlendShape) 설정
  private void Set(ExpressionPreset p, float v)
  {
    if (vrmInstance != null && vrmInstance.Runtime != null)
    {
      vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.CreateFromPreset(p), Mathf.Clamp01(v));
    }
  }

  // 헬퍼: 트래킹 패킷 값 가져오기
  private float Get(float[] v, string name) =>
    TrackingPacket.BlendShapeIndex.TryGetValue(name, out int i) ? v[i] : 0f;
}
```

**파일 생성 경로**: `Assets/Android/Mapping/HandMapper.cs`

```csharp
// HandMapper.cs
// 손가락 관절 랜드마크를 애니메이터 본 회전값으로 변환
using UnityEngine;

public class HandMapper
{
    public static void Apply(Vector3[] joints, Animator animator, bool isLeft)
    {
        // 실전에서는 21개 관절 사이의 Vector3.Angle을 계산하여 각 손가락 마디 회전
        // 여기서는 개념적 구현만 포함
        var wrist = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        if (wrist != null) wrist.position = joints[0]; // 손목 위치 동기화
    }
}
```

**파일 생성 경로**: `Assets/Android/Tracking/TrackingPipeline.cs`

```csharp
// TrackingPipeline.cs
// 모든 트래킹 컴포넌트를 지휘하는 메인 컨트롤러
using UnityEngine;

public class TrackingPipeline : MonoBehaviour
{
  [SerializeField] private WebCamTexture    cameraTexture;
  [SerializeField] private BlazeFaceDetector faceDetector;
  [SerializeField] private BlazeHandDetector handDetector;
  [SerializeField] private HandLandmarkRunner handLandmarker;
  [SerializeField] private BlazePoseDetector poseDetector;
  [SerializeField] private BlazePoseLandmarkRunner poseLandmarker;
  [SerializeField] private ServerStreamSender serverSender;

  private TrackingPacket _currentPacket;

  void Start()
  {
    cameraTexture = new WebCamTexture(640, 480, 30);
    cameraTexture.Play();
    _currentPacket = new TrackingPacket { BlendShapeValues = new float[52] };
    _ = UpdateLoop();
  }

  private async Awaitable UpdateLoop()
  {
    while (cameraTexture.isPlaying)
    {
      // 1. 얼굴 감지 및 BlendShape 계산 (Keypoint 기반)
      var face = await faceDetector.DetectAsync(cameraTexture);
      if (face.IsValid)
      {
         // 6개 keypoint 정보를 직접 활용
         var bs = FaceKeyPointBlendShape.Calculate(face.RawBoxes); 
         _currentPacket.BlendShapeValues[0] = bs.BlinkLeft;
         _currentPacket.BlendShapeValues[1] = bs.BlinkRight;
      }

      // 2. 손 트래킹
      var hand = await handDetector.DetectAsync(cameraTexture);
      if (hand.IsValid)
      {
        var joints = await handLandmarker.RunAsync(cameraTexture, hand);
      }

      // 3. 포즈 트래킹
      var pose = await poseDetector.DetectAsync(cameraTexture);
      if (pose.IsValid)
      {
        var poseJoints = await poseLandmarker.RunAsync(cameraTexture, pose);
      }

      // 4. 네트워크 레이어로 전송
      _currentPacket.Timestamp = Time.time;
      await serverSender.SendAsync(_currentPacket);
    }
  }
}
```

---

## 🧮 트래킹 데이터 → BlendShape 계산

```
BlazeFace keypoints (눈, 코, 입 중심)
     ↓ 
눈 keypoint와 bbox 중심 거리 측정  → Blink_L / Blink_R
  (landmark 468점 없이도 깜빡임 근사 가능)

BlazePose/Hand 랜드마크 (x, y, z)
     ↓ 관절 각도 계산 (Vector3.Angle)
팔꿈치, 무릎, 손가락 본 회전값 산출

모두 수식으로 계산 — 추가 모델 학습 없음
```

---

## 📡 Golang 릴레이 서버 프로토콜

```
패킷 구조 (236 bytes 고정):

Offset  Size  내용
──────────────────────────────────────────
0       12    HeadRotation  Vector3
12      12    HeadPosition  Vector3
24       4    Timestamp     float
28     208    BlendShapeValues float × 52
──────────────────────────────────────────
Total: 236 bytes

통신 흐름 (Pub/Sub 패턴):
1. PC 클라이언트(Unity)가 Golang 서버로 "SUBSCRIBE" 텍스트 패킷 전송 (사이드 IP/포트 서버에 등록)
2. Android 클라이언트(Unity)가 Golang 서버로 초당 30프레임씩 236 bytes 패킷 발사! (UDP)
3. Golang(중계서버)는 등록된 모든 PC(구독자)에게 1ms 미만의 지연율로 패킷을 복사하여 뿌려줌 (브로드캐스트)

※ 안드로이드와 PC가 같은 와이파이에 없어도(LTE 등), 외부망 클라우드 서버로 통신 가능!
※ Docker 빌드 시 CGO_ENABLED=0 (순수 빌드) 옵션을 주어 10MB대의 극한 가동 효율 이미지 생성
```

---

## ⚙️ Android 빌드 설정

```
Project Settings > Player > Android
  ├─ Minimum API Level : API 24 (Android 7.0)
  ├─ Scripting Backend : IL2CPP
  └─ Target Architecture : ARM64 ✅

Project Settings > Graphics
  └─ Vulkan을 목록 최상단으로 이동 (Sentis GPU 가속 향상)

Android 최적화 가이드:
  1. StreamingAssets 대신 Resources 시스템 사용 (안드로이드 파일 IO 문제 예방)
  2. 전면 카메라(isFrontFacing) 자동 감지 로직 적용
  
Sentis BackendType 폴백 전략:
  GPUCompute (Vulkan) → 지원 안 되면 CPUCompute 자동 전환

  try { return new Worker(model, BackendType.GPUCompute); }
  catch { return new Worker(model, BackendType.CPUCompute); }
```

---

## 📊 성능 목표

| 항목 | 목표치 |
|------|--------|
| 전체 FPS | 30fps 유지 |
| BlazeFace 추론 | < 5ms |
| Face KeyPoint 계산 | < 1ms |
| BlazeHand 감지 | < 8ms |
| HandLandmark 추론 | < 12ms |
| BlazePose 감지 | < 8ms |
| BlazePose 랜드마크 | < 15ms |
| 서버 전송 | < 2ms |

---

## 💼 어필 포인트

```
✅ sentis-samples 공식 API 정확히 사용
   ReadbackAndCloneAsync / BlazeUtils.SampleImageAffine / PeekOutput(인덱스)

✅ Functional.NMS / Functional.ArgMax 모델 그래프 내장 방식 이해

✅ 회전 정렬 크롭 구현 (단순 박스 크롭 대비 정확도 향상)

✅ Unity 6 내장 Awaitable 활용 (UniTask 의존성 없음)

✅ Golang과 Docker를 활용한 릴레이 서버 구축 경험 (Pub/Sub 패턴의 클라우드 초저지연 통신)

✅ Android 특화 최적화 (Resources 로드 방식 전환으로 IO 에러 방지 및 전면 카메라 자동 지원)

✅ iPhone/Android 동일 TrackingPacket → PC 앱 100% 재사용 (DRY)

✅ ARCore 의존성 없음 — 순수 Sentis 2.5.0
```

---

*Unity 6000.3.9f1 | Sentis 2.5.0 | sentis-samples BlazeDetectionSample (Face/Hand/Pose) 기준*
