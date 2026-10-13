# Sync latest code to GitHub (no shared PAT)

Your GitHub repo: https://github.com/VEDANSHKUMAR/schwab-ai-assessment-agentic-url-shortener

Because the previous PAT must not be reused, update GitHub with **GitHub CLI browser login** on your PC:

```powershell
cd C:\Users\DELL\schwab-ai-assessment-agentic-url-shortener   # or clone fresh
# If you don't have the latest files yet, zip/clone from Cursor agent workspace first.

gh auth login -h github.com -p https -w
git remote add github https://github.com/VEDANSHKUMAR/schwab-ai-assessment-agentic-url-shortener.git 2>$null
git push -u github main
```

Or run: `scripts/push-github.ps1`

If GitHub rejects `.github/workflows/ci.yml`, re-login with the **workflow** scope enabled, or temporarily commit without that folder.
