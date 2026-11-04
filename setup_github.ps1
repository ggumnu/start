Write-Host "==================================================="
Write-Host "  Mob_archive 프로젝트 Git/GitHub 초기 설정"
Write-Host "==================================================="
Write-Host ""

# 1) Git 설치 확인
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "[오류] Git이 설치되어 있지 않습니다."
    Write-Host "https://git-scm.com/download/win 에서 설치 후 다시 실행해주세요."
    Read-Host "엔터를 누르면 창이 닫힙니다"
    exit 1
}

# 2) Git 사용자 정보 확인 (최초 1회만 물어봄)
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

# 3) Git 저장소 초기화 (이미 되어 있으면 건너뜀)
if (Test-Path ".git") {
    Write-Host "이미 Git 저장소로 초기화되어 있습니다. 건너뜁니다."
} else {
    Write-Host "Git 저장소를 초기화합니다..."
    git init
    git branch -M main
}

Write-Host ""
Write-Host "GitHub에서 만든 '빈' 저장소의 URL을 붙여넣고 Enter를 누르세요."
Write-Host "예시: https://github.com/아이디/저장소이름.git"
$repoUrl = Read-Host "URL"

Write-Host ""
Write-Host "원격 저장소를 연결합니다..."
git remote remove origin 2>$null
git remote add origin "$repoUrl"

Write-Host ""
Write-Host "파일을 추가합니다 (.gitignore에 따라 Library/Temp/obj 등은 자동 제외됩니다)..."
Write-Host "(파일이 많아 몇 분 걸릴 수 있습니다.)"
git add .

Write-Host ""
Write-Host "첫 커밋을 생성합니다..."
git commit -m "Initial commit"

Write-Host ""
Write-Host "GitHub로 업로드합니다... 로그인 창이 뜨면 평소 쓰던 GitHub 계정으로 로그인해주세요."
git push -u origin main

Write-Host ""
if ($LASTEXITCODE -ne 0) {
    Write-Host "==================================================="
    Write-Host "  업로드 중 문제가 발생했습니다. 아래를 확인해보세요:"
    Write-Host "  - 붙여넣은 저장소 URL이 정확한지"
    Write-Host "  - GitHub 로그인이 정상적으로 완료됐는지"
    Write-Host "  - GitHub에서 저장소를 만들 때 README/gitignore 등을 체크하지 않고 완전히 빈 상태로 만들었는지"
    Write-Host "==================================================="
} else {
    Write-Host "==================================================="
    Write-Host "  완료되었습니다! 이제 GitHub에서 확인해보세요."
    Write-Host "==================================================="
}
Write-Host ""
Read-Host "엔터를 누르면 창이 닫힙니다"
