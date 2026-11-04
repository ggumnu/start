Write-Host "==========================================="
Write-Host "  Mob_archive 프로젝트 처음 받아오기 (클론)"
Write-Host "==========================================="
Write-Host ""

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "[오류] Git이 설치되어 있지 않습니다."
    Write-Host "https://git-scm.com/download/win 에서 설치 후 다시 실행해주세요."
    Read-Host "엔터를 누르면 창이 닫힙니다"
    exit 1
}

$gitName = git config --global user.name
if ([string]::IsNullOrWhiteSpace($gitName)) {
    Write-Host "Git을 처음 사용하시는 것 같습니다."
    $gitName = Read-Host "사용할 이름을 입력하세요 (예: 형빈)"
    git config --global user.name "$gitName"
}
$gitEmail = git config --global user.email
if ([string]::IsNullOrWhiteSpace($gitEmail)) {
    $gitEmail = Read-Host "사용할 이메일을 입력하세요 (GitHub 계정 이메일 권장)"
    git config --global user.email "$gitEmail"
}

Write-Host ""
Write-Host "GitHub 저장소의 URL을 붙여넣고 Enter를 누르세요."
Write-Host "예시: https://github.com/아이디/저장소이름.git"
$repoUrl = Read-Host "URL"

Write-Host ""
Write-Host "이 프로젝트를 받을 폴더 이름을 입력하세요 (그냥 Enter 누르면 'Mob_archive'로 생성됩니다)"
$folderName = Read-Host "폴더 이름"
if ([string]::IsNullOrWhiteSpace($folderName)) {
    $folderName = "Mob_archive"
}

if (Test-Path $folderName) {
    Write-Host "[오류] '$folderName' 폴더가 이미 이 위치에 존재합니다."
    Write-Host "다른 이름을 입력하거나, 기존 폴더를 지우거나 다른 위치로 옮긴 뒤 다시 실행해주세요."
    Read-Host "엔터를 누르면 창이 닫힙니다"
    exit 1
}

Write-Host ""
Write-Host "프로젝트를 다운로드합니다... (용량에 따라 몇 분 걸릴 수 있습니다)"
Write-Host "중간에 로그인 창이 뜨면 평소 쓰시던 GitHub 계정으로 로그인해주세요."
git clone "$repoUrl" "$folderName"

Write-Host ""
if ($LASTEXITCODE -ne 0) {
    Write-Host "==========================================="
    Write-Host "  다운로드 중 문제가 발생했습니다. 화면에 뜬 메시지를 캡처해서 알려주세요."
    Write-Host "==========================================="
} else {
    Write-Host "==========================================="
    Write-Host "  완료! '$folderName' 폴더가 만들어졌습니다."
    Write-Host "  이 폴더를 Unity Hub에서 '프로젝트 열기'로 열면 됩니다."
    Write-Host "  이후부터는 이 폴더 안에 있는 저장하기.bat / 최신버전_가져오기.bat 를"
    Write-Host "  그대로 쓰시면 됩니다 (지금 이 스크립트는 다시 쓸 필요 없습니다)."
    Write-Host "==========================================="
}
Write-Host ""
Read-Host "엔터를 누르면 창이 닫힙니다"
