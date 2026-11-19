/**
 * PinNote Landing Page — Interactive Engine
 * OS Detection, Live Sticky Note Simulator, Feature Tabs, Dark/Light Mode, Copy CLI
 */

document.addEventListener('DOMContentLoaded', () => {
  initTheme();
  initOsDetection();
  initAutoReleaseSync();
  initStickyNotePlayground();
  initFeatureTabs();
  initFaqAccordion();
  initCopyButtons();
});

/* ==========================================================================
   1. Theme Management (Dark / Light)
   ========================================================================== */

function initTheme() {
  const toggleBtn = document.getElementById('theme-toggle-btn');
  const savedTheme = localStorage.getItem('pinnote-theme');

  // Always default to bright/light mode, with option to switch to dark mode
  const currentTheme = savedTheme === 'dark' ? 'dark' : 'light';
  document.documentElement.setAttribute('data-theme', currentTheme);
  updateThemeIcon(currentTheme);

  if (toggleBtn) {
    toggleBtn.addEventListener('click', () => {
      const active = document.documentElement.getAttribute('data-theme');
      const nextTheme = active === 'dark' ? 'light' : 'dark';
      document.documentElement.setAttribute('data-theme', nextTheme);
      localStorage.setItem('pinnote-theme', nextTheme);
      updateThemeIcon(nextTheme);
    });
  }
}

function updateThemeIcon(theme) {
  const icon = document.getElementById('theme-icon');
  const toggleBtn = document.getElementById('theme-toggle-btn');
  if (!icon) return;

  if (theme === 'dark') {
    // In dark mode, show Sun icon indicating option to switch back to bright mode
    icon.innerHTML = `<path d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none"/>`;
    if (toggleBtn) toggleBtn.title = 'Switch to Bright Mode';
  } else {
    // In bright mode, show Moon icon indicating option to switch to dark mode
    icon.innerHTML = `<path d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" fill="none"/>`;
    if (toggleBtn) toggleBtn.title = 'Switch to Dark Mode';
  }
}

/* ==========================================================================
   2. Intelligent OS Detection & Download Customization
   ========================================================================== */

function initOsDetection() {
  const ua = navigator.userAgent || navigator.platform || '';
  const isWindows = /Win/i.test(ua);
  const isLinux = /Linux/i.test(ua) && !/Android/i.test(ua);
  const isMac = /Mac/i.test(ua);

  const smartBtn = document.getElementById('hero-smart-download-btn');
  const smartLabel = document.getElementById('hero-smart-download-label');
  const smartSubtext = document.getElementById('hero-smart-download-subtext');
  const smartIcon = document.getElementById('hero-smart-download-icon');

  const windowsCard = document.getElementById('platform-card-windows');
  const linuxCard = document.getElementById('platform-card-linux');

  // Fallback defaults
  let targetUrl = 'https://github.com/Tauhid3K/PinNote/releases/download/v1.0.0/PinNote-Setup-1.0.0.exe';
  let targetOs = 'Windows';

  if (isLinux) {
    targetUrl = 'https://github.com/Tauhid3K/PinNote/releases/download/v1.0.0/PinNote-Setup-1.0.0.sh';
    targetOs = 'Linux';

    if (smartLabel) smartLabel.textContent = 'Download for Linux (v1.0.0)';
    if (smartSubtext) smartSubtext.textContent = 'Standalone .sh Installer & .tar.gz (x64)';
    if (smartIcon) {
      smartIcon.innerHTML = `<path d="M12.5 2C10.5 2 9 3.5 9 5.5v2.8C7.5 9 6.5 10.8 6.5 13c0 1.2.3 2.3.8 3.2L6 20h3l1.2-2.5c.6.3 1.2.5 1.8.5s1.2-.2 1.8-.5L15 20h3l-1.3-3.8c.5-.9.8-2 .8-3.2 0-2.2-1-4-2.5-4.7V5.5c0-2-1.5-3.5-3.5-3.5zm-1 5c-.6 0-1-.4-1-1s.4-1 1-1 1 .4 1 1-.4 1-1 1zm2 0c-.6 0-1-.4-1-1s.4-1 1-1 1 .4 1 1-.4 1-1 1z" fill="currentColor"/>`;
    }

    if (linuxCard) linuxCard.classList.add('detected-os');
  } else {
    // Windows default or other
    targetUrl = 'https://github.com/Tauhid3K/PinNote/releases/download/v1.0.0/PinNote-Setup-1.0.0.exe';
    targetOs = 'Windows';

    if (smartLabel) smartLabel.textContent = 'Download for Windows (v1.0.0)';
    if (smartSubtext) smartSubtext.textContent = 'Self-contained 64-bit Setup .exe • No .NET needed';
    if (smartIcon) {
      smartIcon.innerHTML = `<path d="M0 3.449L9.75 2.1v9.451H0m10.949-9.602L24 0v11.4H10.949M0 12.6h9.75v9.451L0 20.699M10.949 12.6H24V24l-12.9-1.801" fill="currentColor"/>`;
    }

    if (windowsCard) windowsCard.classList.add('detected-os');
  }

  if (smartBtn) {
    smartBtn.href = targetUrl;
  }
}

/* ==========================================================================
   2b. Automatic GitHub Release Synchronization
   Fetches the latest release info from GitHub REST API and automatically updates
   all download links, filenames, version badges, and OS Smart CTAs in real-time.
   ========================================================================== */

async function initAutoReleaseSync() {
  const repo = 'Tauhid3K/PinNote';
  try {
    const res = await fetch(`https://api.github.com/repos/${repo}/releases/latest`, {
      headers: { 'Accept': 'application/vnd.github.v3+json' }
    });
    if (!res.ok) return;

    const data = await res.json();
    if (!data || !data.tag_name) return;

    const versionTag = data.tag_name.trim(); // e.g. "v1.0.0"
    const cleanVersion = versionTag.replace(/^v/i, '');
    const assets = Array.isArray(data.assets) ? data.assets : [];

    // Find binary assets dynamically
    const winExe = assets.find(a => a.name.endsWith('.exe'));
    const winZip = assets.find(a => a.name.endsWith('.zip'));
    const linuxSh = assets.find(a => a.name.endsWith('.sh'));
    const linuxTar = assets.find(a => a.name.endsWith('.tar.gz') || a.name.endsWith('.tgz'));

    // Dynamically update Windows download cards
    if (winExe) {
      document.querySelectorAll('a[data-asset="win-exe"]').forEach(a => {
        a.href = winExe.browser_download_url;
      });
    }
    if (winZip) {
      document.querySelectorAll('a[data-asset="win-zip"]').forEach(a => {
        a.href = winZip.browser_download_url;
      });
    }

    // Dynamically update Linux download cards
    if (linuxSh) {
      document.querySelectorAll('a[data-asset="linux-sh"]').forEach(a => {
        a.href = linuxSh.browser_download_url;
      });
    }
    if (linuxTar) {
      document.querySelectorAll('a[data-asset="linux-tar"]').forEach(a => {
        a.href = linuxTar.browser_download_url;
      });
    }

    // Dynamically update Hero Smart CTA based on client OS
    const ua = navigator.userAgent || navigator.platform || '';
    const isLinux = /Linux/i.test(ua) && !/Android/i.test(ua);
    const smartBtn = document.getElementById('hero-smart-download-btn');
    const smartLabel = document.getElementById('hero-smart-download-label');
    const smartSubtext = document.getElementById('hero-smart-download-subtext');

    if (isLinux && linuxSh) {
      if (smartBtn) smartBtn.href = linuxSh.browser_download_url;
      if (smartLabel) smartLabel.textContent = `Download for Linux (v${cleanVersion})`;
      if (smartSubtext) smartSubtext.textContent = `Standalone .sh Installer & .tar.gz (x64)`;
    } else if (!isLinux && winExe) {
      if (smartBtn) smartBtn.href = winExe.browser_download_url;
      if (smartLabel) smartLabel.textContent = `Download for Windows (v${cleanVersion})`;
      if (smartSubtext) smartSubtext.textContent = `Self-contained 64-bit Setup .exe • No .NET needed`;
    }

    // Dynamically update version badges and headings across page
    document.querySelectorAll('[data-version-display]').forEach(el => {
      el.textContent = `${cleanVersion} (Official Release)`;
    });

    const versionPill = document.getElementById('hero-version-pill-text');
    if (versionPill) {
      versionPill.textContent = `Version ${cleanVersion} Released • Cross-Platform Native`;
    }

    const versionPillLink = document.getElementById('hero-release-pill-link');
    if (versionPillLink && data.html_url) {
      versionPillLink.href = data.html_url;
    }

    const sectionTitle = document.getElementById('download-section-title');
    if (sectionTitle) {
      sectionTitle.textContent = `Download PinNote v${cleanVersion}`;
    }
  } catch (err) {
    // Keep standard fallback defaults if offline or unauthenticated
    console.debug('Release auto-sync fallback preserved:', err);
  }
}

/* ==========================================================================
   3. Interactive Sticky Note Playground (Hero Simulator)
   ========================================================================== */

function initStickyNotePlayground() {
  const noteCard = document.getElementById('demo-note-card');
  const pinBtn = document.getElementById('demo-pin-btn');
  const crystalToggle = document.getElementById('demo-crystal-clear');
  const modeButtons = document.querySelectorAll('.mode-btn');
  const panes = document.querySelectorAll('.note-pane');
  const colorSwatches = document.querySelectorAll('.color-swatch-dot');
  const checklistItems = document.querySelectorAll('.checklist-item');

  if (!noteCard) return;

  // Mode switching (Text / Checklist / Markdown)
  modeButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const targetMode = btn.getAttribute('data-mode');

      modeButtons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');

      panes.forEach(pane => {
        pane.classList.remove('active');
        if (pane.id === `pane-${targetMode}`) {
          pane.classList.add('active');
        }
      });
    });
  });

  // Color Swatch Selection
  const colorMap = {
    yellow: { bg: '#fef08a', text: '#1e293b' },
    peach: { bg: '#fed7aa', text: '#1e293b' },
    mint: { bg: '#bbf7d0', text: '#1e293b' },
    sky: { bg: '#bae6fd', text: '#1e293b' },
    lavender: { bg: '#ddd6fe', text: '#1e293b' },
    charcoal: { bg: '#1e293b', text: '#f8fafc' }
  };

  colorSwatches.forEach(swatch => {
    swatch.addEventListener('click', () => {
      const color = swatch.getAttribute('data-color');
      colorSwatches.forEach(s => s.classList.remove('active'));
      swatch.classList.add('active');

      if (colorMap[color]) {
        if (!noteCard.classList.contains('crystal-clear')) {
          noteCard.style.backgroundColor = colorMap[color].bg;
        }
        noteCard.style.color = colorMap[color].text;
        noteCard.setAttribute('data-active-color', color);
      }
    });
  });

  // Pin Always On Top Toggle
  if (pinBtn) {
    pinBtn.addEventListener('click', () => {
      const isPinned = pinBtn.classList.toggle('pinned');
      pinBtn.title = isPinned ? 'Pinned: Always on Top' : 'Unpinned';
      showTemporaryToast(isPinned ? '📌 Note Pinned Always-on-Top' : 'Unpinned Note');
    });
  }

  // Crystal Clear Mode Toggle
  if (crystalToggle) {
    crystalToggle.addEventListener('change', (e) => {
      if (e.target.checked) {
        noteCard.classList.add('crystal-clear');
      } else {
        noteCard.classList.remove('crystal-clear');
        const activeColor = noteCard.getAttribute('data-active-color') || 'yellow';
        noteCard.style.backgroundColor = colorMap[activeColor].bg;
      }
    });
  }

  // Checklist Item Toggle
  checklistItems.forEach(item => {
    const checkbox = item.querySelector('input[type="checkbox"]');
    if (checkbox) {
      checkbox.addEventListener('change', () => {
        if (checkbox.checked) {
          item.classList.add('checked');
        } else {
          item.classList.remove('checked');
        }
      });
    }
  });
}

function showTemporaryToast(message) {
  let toast = document.getElementById('demo-toast');
  if (!toast) {
    toast = document.createElement('div');
    toast.id = 'demo-toast';
    toast.style.cssText = `
      position: absolute;
      bottom: -45px;
      left: 50%;
      transform: translateX(-50%);
      background: #1e293b;
      color: #f8fafc;
      padding: 6px 14px;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 600;
      box-shadow: 0 4px 14px rgba(0,0,0,0.3);
      border: 1px solid rgba(255,255,255,0.1);
      opacity: 0;
      transition: opacity 0.2s ease, transform 0.2s ease;
      pointer-events: none;
      z-index: 10;
    `;
    const col = document.querySelector('.hero-interactive-col');
    if (col) col.appendChild(toast);
  }

  toast.textContent = message;
  toast.style.opacity = '1';
  toast.style.transform = 'translateX(-50%) translateY(0)';

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(-50%) translateY(6px)';
  }, 2000);
}

/* ==========================================================================
   4. Feature Deep-Dive Tabs
   ========================================================================== */

function initFeatureTabs() {
  const tabButtons = document.querySelectorAll('.deep-tab-btn');
  const tabPanes = document.querySelectorAll('.tab-pane');

  tabButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const targetTab = btn.getAttribute('data-tab');

      tabButtons.forEach(b => b.classList.remove('active'));
      btn.classList.add('active');

      tabPanes.forEach(pane => {
        pane.classList.remove('active');
        if (pane.id === `tab-pane-${targetTab}`) {
          pane.classList.add('active');
        }
      });
    });
  });
}

/* ==========================================================================
   5. FAQ Accordion
   ========================================================================== */

function initFaqAccordion() {
  const faqItems = document.querySelectorAll('.faq-item');

  faqItems.forEach(item => {
    const question = item.querySelector('.faq-question');
    if (question) {
      question.addEventListener('click', () => {
        const isOpen = item.classList.contains('open');

        // Close other items
        faqItems.forEach(other => other.classList.remove('open'));

        // Toggle current
        if (!isOpen) {
          item.classList.add('open');
        }
      });
    }
  });
}

/* ==========================================================================
   6. Copy-to-Clipboard for CLI / One-Liners
   ========================================================================== */

function initCopyButtons() {
  const copyButtons = document.querySelectorAll('.copy-btn');

  copyButtons.forEach(btn => {
    btn.addEventListener('click', () => {
      const textToCopy = btn.getAttribute('data-copy');
      if (!textToCopy) return;

      navigator.clipboard.writeText(textToCopy).then(() => {
        const originalText = btn.textContent;
        btn.textContent = 'Copied! ✓';
        btn.style.color = '#10b981';
        btn.style.borderColor = '#10b981';

        setTimeout(() => {
          btn.textContent = originalText;
          btn.style.color = '';
          btn.style.borderColor = '';
        }, 2000);
      }).catch(err => {
        console.error('Failed to copy: ', err);
      });
    });
  });
}
