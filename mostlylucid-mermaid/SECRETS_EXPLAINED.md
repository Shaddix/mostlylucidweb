# GitHub Secrets Explained

## Quick Answer

**You only need to add ONE secret: `NPM_TOKEN`**

Everything else is automatic!

## The Two Tokens

### `GITHUB_TOKEN` (Automatic ✅)

```yaml
env:
  GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

**Status:** 🤖 **Automatically provided by GitHub**

- ✅ **No setup required**
- ✅ Already exists in every workflow
- ✅ Created fresh for each workflow run
- ✅ Scoped to your repository

**Used for:**
- Creating GitHub Releases
- Commenting on PRs
- Updating repository content
- Any GitHub API operations

**Permissions:**
Controlled by the `permissions:` block in the workflow:
```yaml
permissions:
  contents: write  # Can create releases
  id-token: write  # Can sign packages (provenance)
```

---

### `NPM_TOKEN` (Manual ❌)

```yaml
env:
  NODE_AUTH_TOKEN: ${{ secrets.NPM_TOKEN }}
```

**Status:** 👤 **You must add this manually**

- ❌ **Requires setup** (one-time)
- ❌ Must be created at npmjs.com
- ❌ Must be added to GitHub Secrets

**Used for:**
- Publishing packages to npm
- Authenticating with npm registry

**How to add:**
1. Go to npmjs.com → Access Tokens
2. Create "Automation" token
3. Copy the token
4. Go to GitHub repo → Settings → Secrets → Actions
5. Add secret named `NPM_TOKEN`
6. Paste the token value

---

## Visual Guide

```
┌─────────────────────────────────────────────────────────────┐
│                    GitHub Actions Workflow                   │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Publishing to npm:                                          │
│  ┌────────────────────────────────────────────────────┐    │
│  │ npm publish                                         │    │
│  │ Uses: NPM_TOKEN (you add this) ─────────────────┐ │    │
│  └────────────────────────────────────────────────┼──┘    │
│                                                     │       │
│                                                     v       │
│                                          ┌──────────────┐  │
│                                          │   npmjs.com  │  │
│                                          └──────────────┘  │
│                                                             │
│  Creating GitHub Release:                                   │
│  ┌────────────────────────────────────────────────────┐   │
│  │ create-release                                      │   │
│  │ Uses: GITHUB_TOKEN (automatic) ─────────────────┐ │   │
│  └────────────────────────────────────────────────┼──┘   │
│                                                     │      │
│                                                     v      │
│                                          ┌──────────────┐ │
│                                          │GitHub Release│ │
│                                          └──────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Common Confusion

### ❓ "Why does it say `secrets.GITHUB_TOKEN`?"

Even though it's in the `secrets` object, it's **automatically populated** by GitHub. You don't add it.

Think of it like this:
```javascript
// GitHub automatically does this for you:
secrets = {
  GITHUB_TOKEN: "automatically-generated-token-abc123",
  NPM_TOKEN: process.env.NPM_TOKEN  // This one you add
}
```

### ❓ "Do I need to create GITHUB_TOKEN?"

**NO!** It's already there. If you try to add it manually, GitHub will ignore it and use the automatic one anyway.

### ❓ "What if I see 'GITHUB_TOKEN not found'?"

This shouldn't happen in modern GitHub Actions. If it does:
1. Check your workflow permissions (add `contents: write`)
2. Make sure you're on a public repo or have Actions enabled

### ❓ "Can I use GITHUB_TOKEN to publish to npm?"

**NO!** `GITHUB_TOKEN` only works for GitHub operations. For npm, you need `NPM_TOKEN`.

## Checklist for Setup

- [ ] Create npm token at npmjs.com
- [ ] Add `NPM_TOKEN` to GitHub Secrets
- [ ] ✅ `GITHUB_TOKEN` is automatic - skip this!
- [ ] Push a tag to trigger workflow

## Verification

After setup, you should have:

**In GitHub Secrets:**
```
NPM_TOKEN: npm_xxxxxxxxxx (you added this)
```

**In Workflow (automatic):**
```
GITHUB_TOKEN: ghs_xxxxxxxxxx (GitHub adds this)
```

## Troubleshooting

### Publishing fails with "401 Unauthorized"

**Cause:** `NPM_TOKEN` is missing or invalid

**Fix:**
1. Verify token exists at npmjs.com
2. Regenerate if needed
3. Update GitHub Secret

### Release creation fails with "403 Forbidden"

**Cause:** Missing permissions

**Fix:** Add to workflow:
```yaml
permissions:
  contents: write
```

### Token shows in logs

**Don't worry!** GitHub automatically masks all secrets in logs. You'll see `***` instead of the actual token.

## Security Notes

- ✅ Both tokens are automatically masked in logs
- ✅ `GITHUB_TOKEN` expires after the workflow completes
- ✅ `NPM_TOKEN` should be "Automation" type (not "Publish")
- ✅ Enable 2FA on your npm account
- ✅ Rotate `NPM_TOKEN` every 3-6 months

## Summary

| Token | Setup | Used For | Lifetime |
|-------|-------|----------|----------|
| `GITHUB_TOKEN` | Automatic ✅ | GitHub operations | Per workflow run |
| `NPM_TOKEN` | Manual ❌ | npm publishing | Until revoked |

**Bottom line:** You only need to add `NPM_TOKEN`. Everything else is automatic! 🎉
