Write-Host "==================================================="
Write-Host "  Mob_archive 변경사항 GitHub에 저장"
Write-Host "==================================================="
Write-Host ""

if (-not (Test-Path ".git")) {
    Write-Host "[오류] 이 폴더는 아직 Git 저장소로 설정되어 있지 않습니다."
    Read-Host "엔터를 누르면 창이 닫힙니다"
    exit 1
}

git add .

$status = git status --porcelain
if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host "저장할 변경사항이 없습니다. (이미 최신 상태입니다)"
    Read-Host "엔터를 누르면 창이 닫힙니다"
    exit 0
}

Write-Host "변경된 내용:"
git status --short
Write-Host ""

$msg = Read-Host "이번 작업에 대한 간단한 설명을 입력하세요 (예: 몬스터 이동속도 조정 / 그냥 Enter만 눌러도 됩니다)"
if ([string]::IsNullOrWhiteSpace($msg)) {
    $msg = "작업 저장 " + (Get-Date -Format "yyyy-MM-dd HH:mm")
}

git commit -m "$msg"

Write-Host ""
Write-Host "GitHub로 업로드합니다..."
git push

Write-Host ""
if ($LASTEXITCODE -ne 0) {
    Write-Host "==================================================="
    Write-Host "  업로드 중 문제가 발생했습니다."
    Write-Host "  다른 컴퓨터에서 먼저 작업하고 이 컴퓨터로 아직 받아오지 않았다면,"
    Write-Host "  먼저 '최신버전_가져오기'를 실행한 뒤 다시 시도해주세요."
    Write-Host "==================================================="
} else {
    Write-Host "==================================================="
    Write-Host "  저장 완료! GitHub에 안전하게 업로드되었습니다."
    Write-Host "==================================================="
}
Write-Host ""
Read-Host "엔터를 누르면 창이 닫힙니다"
