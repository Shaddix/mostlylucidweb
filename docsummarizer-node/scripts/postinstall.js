#!/usr/bin/env node

/**
 * Postinstall script for @mostlylucid/docsummarizer
 * 
 * Downloads the platform-specific docsummarizer binary from GitHub releases.
 * 
 * Platform mapping:
 * - win32 + x64    -> docsummarizer-win-x64.zip
 * - win32 + arm64  -> docsummarizer-win-arm64.zip
 * - linux + x64    -> docsummarizer-linux-x64.tar.gz
 * - linux + arm64  -> docsummarizer-linux-arm64.tar.gz
 * - darwin + x64   -> docsummarizer-osx-x64.tar.gz (Intel Mac)
 * - darwin + arm64 -> docsummarizer-osx-arm64.tar.gz (Apple Silicon)
 */

const https = require('https');
const http = require('http');
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');
const { createUnzip } = require('zlib');

// Configuration
const GITHUB_REPO = 'scottgal/mostlylucidweb';
const RELEASE_TAG_PREFIX = 'docsummarizer-v';

// Skip download in CI or when DOCSUMMARIZER_SKIP_DOWNLOAD is set
if (process.env.CI || process.env.DOCSUMMARIZER_SKIP_DOWNLOAD) {
  console.log('Skipping binary download (CI or DOCSUMMARIZER_SKIP_DOWNLOAD set)');
  process.exit(0);
}

// Paths
const vendorDir = path.join(__dirname, '..', 'vendor');
const isWindows = process.platform === 'win32';
const exeName = isWindows ? 'docsummarizer.exe' : 'docsummarizer';
const exePath = path.join(vendorDir, exeName);

// Skip if already downloaded
if (fs.existsSync(exePath)) {
  console.log(`docsummarizer already installed at ${exePath}`);
  process.exit(0);
}

/**
 * Get the artifact name for the current platform
 */
function getArtifactName() {
  const platform = process.platform;
  const arch = process.arch;

  const mapping = {
    'win32-x64': 'docsummarizer-win-x64.zip',
    'win32-arm64': 'docsummarizer-win-arm64.zip',
    'linux-x64': 'docsummarizer-linux-x64.tar.gz',
    'linux-arm64': 'docsummarizer-linux-arm64.tar.gz',
    'darwin-x64': 'docsummarizer-osx-x64.tar.gz',
    'darwin-arm64': 'docsummarizer-osx-arm64.tar.gz',
  };

  const key = `${platform}-${arch}`;
  const artifact = mapping[key];

  if (!artifact) {
    console.warn(`No prebuilt binary for ${platform}/${arch}`);
    console.warn('You can install manually: dotnet tool install -g Mostlylucid.DocSummarizer');
    return null;
  }

  return artifact;
}

/**
 * Fetch JSON from a URL with redirect handling
 */
function fetchJson(url) {
  return new Promise((resolve, reject) => {
    const client = url.startsWith('https') ? https : http;
    
    const options = {
      headers: {
        'User-Agent': 'docsummarizer-npm-postinstall',
        'Accept': 'application/vnd.github.v3+json',
      },
    };

    client.get(url, options, (res) => {
      // Handle redirects
      if (res.statusCode === 301 || res.statusCode === 302) {
        return resolve(fetchJson(res.headers.location));
      }

      if (res.statusCode !== 200) {
        return reject(new Error(`HTTP ${res.statusCode}: ${url}`));
      }

      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          resolve(JSON.parse(data));
        } catch (e) {
          reject(new Error(`Failed to parse JSON from ${url}`));
        }
      });
    }).on('error', reject);
  });
}

/**
 * Download a file to disk with redirect handling
 */
function downloadFile(url, destPath) {
  return new Promise((resolve, reject) => {
    const client = url.startsWith('https') ? https : http;
    
    const options = {
      headers: {
        'User-Agent': 'docsummarizer-npm-postinstall',
        'Accept': 'application/octet-stream',
      },
    };

    client.get(url, options, (res) => {
      // Handle redirects
      if (res.statusCode === 301 || res.statusCode === 302) {
        return resolve(downloadFile(res.headers.location, destPath));
      }

      if (res.statusCode !== 200) {
        return reject(new Error(`HTTP ${res.statusCode}: ${url}`));
      }

      const file = fs.createWriteStream(destPath);
      res.pipe(file);
      file.on('finish', () => {
        file.close();
        resolve();
      });
    }).on('error', (err) => {
      fs.unlink(destPath, () => {}); // Clean up partial download
      reject(err);
    });
  });
}

/**
 * Extract archive (zip or tar.gz)
 */
async function extractArchive(archivePath, destDir) {
  if (archivePath.endsWith('.zip')) {
    // Use system unzip or PowerShell on Windows
    if (isWindows) {
      execSync(`powershell -Command "Expand-Archive -Path '${archivePath}' -DestinationPath '${destDir}' -Force"`, { stdio: 'inherit' });
    } else {
      execSync(`unzip -o "${archivePath}" -d "${destDir}"`, { stdio: 'inherit' });
    }
  } else if (archivePath.endsWith('.tar.gz')) {
    execSync(`tar -xzf "${archivePath}" -C "${destDir}"`, { stdio: 'inherit' });
  } else {
    throw new Error(`Unknown archive format: ${archivePath}`);
  }
}

/**
 * Get the latest release matching our tag prefix
 */
async function getLatestRelease() {
  const url = `https://api.github.com/repos/${GITHUB_REPO}/releases`;
  const releases = await fetchJson(url);
  
  // Find the latest release with our tag prefix
  for (const release of releases) {
    if (release.tag_name && release.tag_name.startsWith(RELEASE_TAG_PREFIX)) {
      return release;
    }
  }
  
  throw new Error(`No release found with tag prefix: ${RELEASE_TAG_PREFIX}`);
}

/**
 * Find the download URL for our artifact in the release
 */
function findAssetUrl(release, artifactName) {
  const asset = release.assets.find(a => a.name === artifactName);
  if (!asset) {
    throw new Error(`Asset ${artifactName} not found in release ${release.tag_name}`);
  }
  return asset.browser_download_url;
}

/**
 * Main installation logic
 */
async function main() {
  console.log('Installing docsummarizer binary...');
  
  const artifactName = getArtifactName();
  if (!artifactName) {
    console.log('Skipping binary download for unsupported platform');
    return;
  }

  console.log(`Platform: ${process.platform}/${process.arch}`);
  console.log(`Artifact: ${artifactName}`);

  try {
    // Get the latest release
    console.log('Fetching latest release...');
    const release = await getLatestRelease();
    console.log(`Found release: ${release.tag_name}`);

    // Find the download URL
    const downloadUrl = findAssetUrl(release, artifactName);
    console.log(`Download URL: ${downloadUrl}`);

    // Create vendor directory
    fs.mkdirSync(vendorDir, { recursive: true });

    // Download the archive
    const archivePath = path.join(vendorDir, artifactName);
    console.log(`Downloading to ${archivePath}...`);
    await downloadFile(downloadUrl, archivePath);
    console.log('Download complete');

    // Extract
    console.log('Extracting...');
    await extractArchive(archivePath, vendorDir);

    // Make executable on Unix
    if (!isWindows && fs.existsSync(exePath)) {
      fs.chmodSync(exePath, 0o755);
    }

    // Clean up archive
    fs.unlinkSync(archivePath);

    // Verify
    if (fs.existsSync(exePath)) {
      console.log(`Installed docsummarizer to ${exePath}`);
    } else {
      throw new Error(`Expected executable not found at ${exePath}`);
    }

  } catch (err) {
    console.error(`Failed to download binary: ${err.message}`);
    console.error('');
    console.error('You can install the CLI manually:');
    console.error('  dotnet tool install -g Mostlylucid.DocSummarizer');
    console.error('');
    console.error('Or set DOCSUMMARIZER_PATH to an existing installation.');
    // Don't fail the npm install - the package can still work with manual installation
    process.exit(0);
  }
}

main();
