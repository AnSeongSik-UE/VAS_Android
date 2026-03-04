# VAS_Android 프로젝트 작업 현황 (task.md)

## 📅 업데이트 날짜: 2026-03-03

### ✅ 완료된 작업
- [x] **카메라 화면 출력 정상화**: `TrackingPipeline.cs`의 레이아웃 로직 수정 및 미러링 보정 완료.
- [x] **얼굴/손 트래킹 OutOfRange 수정**: 앵커 인덱싱 방식(`float[,]`)을 공식 샘플 기준으로 변경하여 런타임 오류 해결.
- [x] **BlazeUtils 공용화**: NMS 및 ArgMax 필터링 로직을 `BlazeUtils`로 통합 및 `InferenceEngine` API 최적화.
- [x] **포즈 트래킹 기본 통합**: `BlazePoseDetector`와 `BlazePoseLandmarkRunner`를 `TrackingPipeline`에 연결.
- [x] **고개 회전(HeadRotation) 계산**: 포즈 랜드마크(코, 귀, 어깨)를 이용한 Pitch/Yaw/Roll 추정 로직 추가.

### 🏃 진행 중인 작업
- [x] **포즈 트래킹 고도화**: 
    - [x] 어깨/팔꿈치 랜드마크를 이용한 팔 회전(ArmRotation) 계산 및 패킷 전송 추가.
    - [x] PC측 `VRMMapper`에서 팔 회전 실시간 적용 로직 구현.
    - [ ] `PoseMapper.cs` 구현 시작 (랜드마크 -> VRM 본 회전 상세화).

### 📋 향후 계획
- [ ] **Golang 릴레이 서버 구축**: `Server/` 폴더 내 `main.go` 및 `Dockerfile` 작성.
- [ ] **네트워크 전송 테스트**: Android 기기에서 보낸 패킷이 서버를 거쳐 PC로 도달하는지 확인.
- [ ] **PC VRM 아바타 연동**: 수신된 데이터를 `VRMMapper`를 통해 아바타에 실시간 적용.
- [ ] **전체 파이프라인 최적화**: 모바일 환경에서의 성능(FPS) 모니터링 및 최적화.

---
*참고: 모든 스크립트는 `StreamingAssets` 대신 `Resources/Data` 경로의 앵커 파일을 사용하도록 변경되었습니다.*

## 작업 로그 (Time-ordered)

### 2026-03-03 — 포즈 트래킹 고도화 및 네트워크 확장
- [x] **팔 회전(Arm Rotation) 연동**: 어깨와 팔꿈치 벡터를 이용한 회전 계산 로직 추가.
- [x] **패킷 구조 확장**: `TrackingPacket`을 236 -> 260바이트(팔 회전 포함)로 확장 및 직렬화 업데이트.
- [x] **PC 수신부 업데이트**: `ServerStreamReceiver`, `VRMMapper`에서 확장된 패킷 처리 및 아바타 본 적용 확인.
- [x] **Golang 서버 준비**: 260바이트 중계 지원을 위한 `Server/main.go` 주석 및 로직 점검.
- [x] **도커 실행 스크립트 추가**: `Server/run_server.ps1` 생성 (원클릭 빌드 및 실행 지원).
- [x] **스크립트 영문 현지화**: 한글 깨짐 방지를 위해 `run_server.ps1` 및 `main.go` 출력 메시지 영문화 완료.
- [x] **모바일 안정성 강화 (NRE 해결)**: `TrackingPipeline` 및 모든 Detector(`Face`, `Hand`, `Pose`)에 널 체크(NRE Guard) 및 영문 디버그 로그 적용.
- [x] **네트워크 송신부 예외 처리**: `ServerStreamSender` 초기화 시점(Awake) 변경 및 송신 예외 방어 코드 적용.
- [x] **화면 멈춤(Freeze) 및 성능 최적화**: 트래킹 루프 내 강제 프레임 양보(`NextFrameAsync`) 추가 및 네트워크 송신 비동기 분리로 카메라 끊김 현상 해결.
- [ ] **네트워크 통합 테스트**: 안드로이드 빌드 후 실제 서버를 통한 PC 아바타 구동 테스트 예정.

---

## ⛔ 금지 사항
- **`AndroidManifest.xml` 추가 금지**: `Assets/Plugins/Android/AndroidManifest.xml`을 절대 생성하지 말 것. Unity Player Settings에서 자동 생성되는 매니페스트에 의존.

---

### 2026-03-04 13:41 — 검은 화면 문제 수정 (Antigravity AI)
- [x] **AndroidManifest.xml 삭제**: 잘못 생성된 `Assets/Plugins/Android/AndroidManifest.xml` 삭제. 이 파일은 프로젝트에서 사용하지 않음.
- [x] **금지 사항 추가**: `AndroidManifest.xml` 추가 금지 규칙을 task.md에 기록.
- [x] **런타임 카메라 권한 요청 추가**: `TrackingPipeline.cs`에서 카메라 사용 전 `Application.RequestUserAuthorization(UserAuthorization.WebCam)` 호출하여 Android 런타임 권한 요청 구현.

### 2026-03-04 14:00 — 앱 1초 후 멈춤 문제 수정 (Antigravity AI)
- [x] **BackendType 변경**: 5개 Worker 파일(`BlazeFaceDetector`, `BlazeHandDetector`, `HandLandmarkRunner`, `BlazePoseDetector`, `BlazePoseLandmarkRunner`)에서 `BackendType.GPUCompute` → `BackendType.CPU`로 변경. Android에서 Compute Shader 비호환으로 인한 행(hang) 방지.
- [x] **라운드 로빈 UpdateLoop**: `TrackingPipeline.cs`의 UpdateLoop를 프레임당 1개 모델(Face→Hand→Pose)만 실행하는 라운드 로빈 방식으로 변경. 모바일 과부하 방지.

### 2026-03-04 14:10 — PC 뷰어 프로젝트 현황 분석 (Antigravity AI)
- [x] **PC 코드 확인**: `ServerStreamReceiver.cs`, `VRMMapper.cs` 존재 확인.
- [x] **서버 코드 확인**: `Server/main.go`, `Dockerfile`, `run_server.ps1` 존재 확인.
- [ ] **UniVRM10 설치**: `.unitypackage` Import 필요 (Unity Editor 수동 작업).
- [ ] **VRM 아바타 다운로드**: VRoid Hub 등에서 무료 `.vrm` 파일 다운로드 필요.
- [x] **PC 전용 씬 구성**: PCViewer 씬 생성 + GameObject/컴포넌트 연결 (Unity Editor 수동 작업).

### 2026-03-04 16:15 — PC 뷰어 VRM 뼈대 회전 보정 (Antigravity AI)
- [x] **기본 회전값 오프셋 적용**: `VRMMapper.cs`에서 아바타의 초기 T-Pose 회전값(`_initialLeftArm`, `_initialRightArm`, `_initialNeck`)을 첫 프레임에 저장하여 기준점으로 사용.
- [x] **좌표계 반전 보정**: 전면 카메라(거울 모드) 기준에 맞춰 Android의 Euler 각도를 Unity World에 맞게 축 반전(`-x, -y, z` 또는 `-x, -y, -z`) 적용. 머리가 꺾이거나 팔이 제멋대로 도는 현상 수정.

### 2026-03-04 16:20 — 트래킹 Lost 복귀 처리 (Antigravity AI)
- [x] **TrackingPacket 확장**: 얼굴 감지 여부를 판별하기 위해 패킷의 끝에 `IsTracking` (1 byte boolean) 플래그를 추가. 총 261 bytes로 확장.
- [x] **송수신부 처리**: `TrackingPipeline.cs`에서 `face.IsValid` 상태를 패킷에 기록. `ServerStreamReceiver.cs`에서 261바이트 수신 검사하도록 수정.
- [x] **T-Pose 복귀 (Slerp)**: `VRMMapper.cs`에서 `p.IsTracking == false`일 경우 기존 자세에서 `_initial` 회전값으로 부드럽게 Slerp하여 복귀하고 표정(BlendShape)을 초기화하는 로직 추가.

### 2026-03-04 17:45 — 추가 기능 및 안정화 (Antigravity AI)
- [ ] **PC 타임아웃 처리**: 3초 이상 패킷 미수신 시 자동으로 T-Pose 복귀 로직 구현.
- [ ] **모바일 디버그 시각화**: 기기 화면에 감지된 랜드마크 포인트를 선/점으로 표시하여 트래킹 상태 확인 기능 추가.
- [ ] **본 초기화 안정화**: VRMMapper의 초기 회전값 캡처 시점 개선 (데이터 수신 전 순수 T-Pose 확보).
