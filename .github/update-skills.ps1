#!/usr/bin/env pwsh
<#
.SYNOPSIS
Updates GitHub Copilot Agent Skills
#>

Write-Host "🔄 Updating GitHub Copilot Agent Skills..." -ForegroundColor Cyan

npx skills add OpenHands/skills --skill releasenotes --agent github-copilot --yes
Write-Host "✅ Added OpenHands releasenotes skill" -ForegroundColor Green

npx skills add stripe/ai --agent github-copilot --yes
Write-Host "✅ Added Stripe AI best-practices" -ForegroundColor Green

npx skills add vercel-labs/agent-browser --agent github-copilot --yes
Write-Host "✅ Added Vercel Labs agent-browser" -ForegroundColor Green

npx skills add anthropics/skills --skill frontend-design --agent github-copilot --yes
Write-Host "✅ Added Anthropic frontend-design skill" -ForegroundColor Green

Write-Host "`n✨ GitHub Copilot Agent Skills update complete!" -ForegroundColor Cyan
