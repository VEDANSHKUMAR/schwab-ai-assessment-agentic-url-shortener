# Run on your Windows machine (does NOT embed secrets in the repo).
# Prerequisites: Git + GitHub CLI (https://cli.github.com/) from official installers.
param(
  [string]$Repo = "VEDANSHKUMAR/schwab-ai-assessment-agentic-url-shortener"
)

Write-Host "Logging into GitHub via browser (trusted GitHub CLI flow)..."
gh auth login -h github.com -p https -w

Write-Host "Pushing main to $Repo ..."
git remote remove github 2>$null
git remote add github "https://github.com/$Repo.git"
git push -u github HEAD:main

Write-Host "Done: https://github.com/$Repo"
