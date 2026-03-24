# Monitoring.Blazor

Blazor Server 기반 모니터링 대시보드입니다. 호스트 스냅샷/알림/로그 분석을 포함합니다.

## Requirements
- .NET SDK 9.0+
- SQL Server (예: SQLEXPRESS)

## Quick Start
```powershell
# 복원/빌드

dotnet restore

dotnet build

# 실행

dotnet run
```

기본 접속: `https://localhost:5088` 또는 `http://localhost:5088` (launchSettings 기준)

## Configuration
설정 파일은 `appsettings.json`/`appsettings.Development.json`에 있습니다.

필수 설정:
- `ConnectionStrings:MonitoringDb`

예시:
```json
{
  "ConnectionStrings": {
    "MonitoringDb": "Server=localhost\\SQLEXPRESS;Database=MonitoringDb;User Id=appUser;Password=REPLACE_ME;TrustServerCertificate=True"
  }
}
```

실제 비밀번호는 `appsettings.local.json`에 넣고 로컬에서 덮어쓰는 방식을 권장합니다.

## Database
앱 시작 시 스키마가 없으면 `EnsureCreated()`로 자동 생성됩니다.

생성되는 테이블:
- `HostSnapshots` (스냅샷)
- `AlertEvents` (알림 이력)
- `LogEntries` (로그 분석 결과)

## API (요약)
- `POST /api/monitor/client-message` : 클라이언트 상태 수신
- `GET /api/monitor/history?hostname=HOST&minutes=60` : 호스트 스냅샷 히스토리
- `GET /api/alerts/history?hostname=HOST&take=200` : 알림 히스토리
- `POST /api/monitor/parse-logs` : IIS 로그 업로드 및 분석/저장

## Publish (Windows)
```powershell
# Self-contained 없이 프레임워크 종속 배포

dotnet publish -c Release -o .\publish
```

IIS 배포 시:
- ASP.NET Core Hosting Bundle 설치 필요
- IIS에서 사이트 추가 후 `publish` 폴더를 경로로 지정

## Notes
- DB 연결 실패 시 `ConnectionStrings:MonitoringDb`와 SQL Server 인스턴스를 확인하세요.
- 개발 환경에서 JS interop 오류가 나오면 prerendering 단계에서 호출하지 않도록 `OnAfterRenderAsync`에서 호출해야 합니다.
