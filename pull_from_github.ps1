Write-Host "==================================================="
Write-Host "  Mob_archive 최신 버전 가져오기"
Write-Host "==================================================="
Write-Host ""

if (-not (Test-Path ".git")) {
    Write-Host "[오류] 이 폴더는 아직 Git 저장소로 설정되어 있지 않습니다."
    Read-Host "엔터를 누르면 창이 닫힙니다"
    exit 1
}

$status = git status --porcelain
if (-not [string]::IsNullOrWhiteSpace($status)) {
    Write-Host "[주의] 아직 저장(커밋/업로드)하지 않은 변경사항이 있습니다:"
    git status --short
    Write-Host ""
    Write-Host "먼저 '저장하기'를 실행해서 지금 작업을 GitHub에 올린 뒤에"
    Write-Host "최신 버전을 받아오는 걸 추천합니다. 이 상태로 계속 진행하면 충돌이 날 수 있습니다."
    $answer = Read-Host "그래도 계속 진행할까요? (y/n)"
    if ($answer -ne "y") {
        Write-Host "취소했습니다."
        Read-Host "엔터를 누르면 창이 닫힙니다"
        exit 0
    }
}

Write-Host "최신 버전을 받아옵니다..."
git pull

Write-Host ""
if ($LASTEXITCODE -ne 0) {
    Write-Host "==================================================="
    Write-Host "  받아오는 중 문제가 발생했습니다. 화면에 뜬 메시지를 캡처해서 알려주세요."
    Write-Host "==================================================="
} else {
    Write-Host "==================================================="
    Write-Host "  완료! 최신 버전으로 업데이트되었습니다."
    Write-Host "==================================================="
}
Write-Host ""
Read-Host "엔터를 누르면 창이 닫힙니다"
