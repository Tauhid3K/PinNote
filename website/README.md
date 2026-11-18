# PinNote Landing Page & Download Portal

This directory contains the official, high-performance static landing page for **PinNote** (Desktop Sticky Notes & Productivity Workspace).

Designed specifically for zero-friction hosting on **[Vercel](https://vercel.com/)** with 100/100 Lighthouse performance, dark/light adaptive theming, dynamic operating system detection, and an interactive sticky note simulator.

---

## 🌟 Highlights & Features

- **Dynamic OS Detection**: Automatically identifies whether the visitor is on Windows or Linux and adapts the primary download button with the appropriate asset (`.exe` or `.sh`/`.tar.gz`).
- **Interactive Sticky Note Playground**: Allows visitors to test PinNote's real UI modes (Text, Checklist, Markdown), change pastel colors, toggle "Crystal Clear" frosted-glass acrylic blur, and pin always-on-top right from their browser.
- **Deep-Dive Showcase**: Interactive tabs highlighting Workspaces, Markdown editing, Reminders & Due dates, and Built-in Templates.
- **Comprehensive Download Matrix**: Direct links to v1.0.4 Windows Inno Setup installer, Linux shell installer, tarball archive, Flatpak, and one-liner CLI curl script.
- **Keyboard Shortcuts Cheatsheet**: Styled `<kbd>` shortcuts reference for power users.
- **Zero-Dependency Static Build**: Pure HTML5, modern CSS variables, and vanilla ES6 JavaScript. No heavy bundlers, zero hydration lag, instant loading.

---

## 🚀 Deploying to Vercel

### Method 1: Connect GitHub Repository (Recommended)

1. Go to [vercel.com](https://vercel.com/) and click **"Add New Project"**.
2. Select your repository: **`Tauhid3K/PinNote`**.
3. **Configuration**:
   - If deploying from the repository root: Vercel automatically detects `vercel.json` and serves `website` as the output directory.
   - Or, in **Root Directory**, set: `website`.
4. Click **Deploy**!
   Your site will be live in seconds at `https://<your-project>.vercel.app`.

### Method 2: Vercel CLI

From your terminal:
```bash
# Install Vercel CLI if not already installed
npm i -g vercel

# Navigate to the website folder and deploy
cd website
vercel --prod
```

---

## 💻 Local Preview & Development

You can preview the site locally using any static web server:

```bash
# Using npx serve
cd website
npx serve .

# Or using Python 3
python3 -m http.server 3000
```
Then open `http://localhost:3000` in your browser.

---

## 📁 File Structure

```
website/
├── index.html        # Main landing page markup with OpenGraph & SEO tags
├── styles.css        # Responsive styling, CSS variables, glassmorphism, animations
├── app.js            # OS detection, interactive sticky note, tabs, dark mode, copy CLI
├── vercel.json       # Vercel deployment configuration & static caching rules
├── package.json      # Metadata and local preview scripts
└── assets/
    ├── logo.svg      # Crisp vector logo
    ├── favicon.svg   # Scalable browser favicon
    └── icon.png      # High-contrast 64x64 application icon
```

---

## 🔄 Updating for New Releases

When releasing a new version (e.g. `v1.0.5`):
1. In `website/index.html`: Update the version badge (`v1.0.4` → `v1.0.5`) and the download URLs pointing to GitHub releases.
2. In `website/app.js`: Update the default URLs in `initOsDetection()` if needed.
3. Push to `master` — Vercel will automatically trigger an instant deployment.
