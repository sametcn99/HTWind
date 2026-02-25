import {
  Badge,
  Body1,
  Button,
  Card,
  Caption1,
  FluentProvider,
  Link,
  Spinner,
  Subtitle1,
  Title1,
  makeStyles,
  mergeClasses,
  shorthands,
  tokens,
  webDarkTheme,
} from '@fluentui/react-components'
import {
  ArrowDownload24Regular,
  Code24Regular,
  Desktop24Regular,
  Open24Regular,
  Rocket24Regular,
  WindowWrench24Regular,
} from '@fluentui/react-icons'
import { useEffect, useMemo, useRef, useState } from 'react'
import './App.css'

const GITHUB_REPOSITORY_URL = 'https://github.com/sametcn99/HTWind'
const GITHUB_DISCUSSIONS_URL = 'https://github.com/sametcn99/HTWind/discussions'
const RELEASES_URL = 'https://github.com/sametcn99/HTWind/releases'
const LATEST_RELEASE_API_URL = 'https://api.github.com/repos/sametcn99/HTWind/releases/latest'
const SUPPORT_URL = 'https://sametcc.me/support'
const RELEASE_CACHE_KEY = 'htwind:latest-release'
const RELEASE_CACHE_TTL_MS = 1000 * 60 * 60

type GitHubRelease = {
  tag_name: string
  name: string
  published_at: string
  html_url: string
  assets?: GitHubReleaseAsset[]
}

type GitHubReleaseAsset = {
  id: number
  name: string
  size: number
  browser_download_url: string
}

type CachedReleaseRecord = {
  savedAt: number
  release: GitHubRelease
}

// Windows 11 dark theme surface tokens
// Stroke:  rgba(255,255,255, 0.083)  — subtle border
// Layer 0: rgba(20, 20, 20, ...)      — base/nav
// Layer 1: rgba(30, 30, 30, ...)      — card
// Layer 2: rgba(38, 38, 38, ...)      — elevated card
// Accent:  #0078D4                    — Windows blue
const useStyles = makeStyles({
  page: {
    color: 'rgba(255, 255, 255, 0.956)',
    minHeight: '100vh',
    backgroundColor: 'transparent',
  },
  shell: {
    marginInline: 'auto',
    maxWidth: '1160px',
    paddingTop: '0',
    paddingRight: '20px',
    paddingBottom: '72px',
    paddingLeft: '20px',
    display: 'grid',
    rowGap: '20px',
    fontFamily: '"Segoe UI Variable Text", "Manrope", "Segoe UI", sans-serif',
    '@media (min-width: 1024px)': {
      rowGap: '24px',
    },
  },
  topBar: {
    position: 'sticky',
    top: '0',
    zIndex: '100',
    display: 'flex',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    alignItems: 'center',
    gap: '12px',
    paddingTop: '12px',
    paddingBottom: '12px',
    marginLeft: '-20px',
    marginRight: '-20px',
    paddingLeft: '20px',
    paddingRight: '20px',
    backgroundColor: 'rgba(16, 16, 16, 0.9)',
    backdropFilter: 'blur(28px) saturate(180%)',
    borderBottom: '1px solid rgba(255, 255, 255, 0.06)',
  },
  brand: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '8px',
    color: 'rgba(255, 255, 255, 0.92)',
    fontWeight: tokens.fontWeightSemibold,
    letterSpacing: '-0.01em',
  },
  topLinks: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '8px',
  },
  topLinkButton: {
    backgroundColor: 'rgba(255, 255, 255, 0.055)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.083)'),
    color: 'rgba(255, 255, 255, 0.78)',
  },
  banner: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '8px',
    width: 'fit-content',
    ...shorthands.padding('6px', '12px'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    backgroundColor: 'rgba(0, 120, 212, 0.14)',
    ...shorthands.border('1px', 'solid', 'rgba(96, 174, 255, 0.35)'),
    color: '#60AEFF',
    marginTop: '2rem',
  },
  heroCard: {
    backgroundColor: 'rgba(30, 30, 30, 0.82)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.083)'),
    display: 'grid',
    rowGap: '20px',
    overflow: 'visible',
    '@media (min-width: 900px)': {
      ...shorthands.padding('36px'),
      rowGap: '24px',
    },
  },
  heroTopRow: {
    display: 'grid',
    gap: '20px',
    '@media (min-width: 900px)': {
      gridTemplateColumns: '1fr auto',
      alignItems: 'start',
    },
  },
  heroHeading: {
    marginTop: '0',
    marginBottom: '0',
    fontFamily: '"Segoe UI Variable Display", "Space Grotesk", "Trebuchet MS", sans-serif',
    fontSize: 'clamp(2rem, 4vw, 3.2rem)',
    letterSpacing: '-0.025em',
    lineHeight: '1.08',
    color: '#ffffff',
    fontWeight: '700',
  },
  heroDescription: {
    marginTop: '12px',
    marginBottom: '0',
    fontSize: '1.03rem',
    color: 'rgba(255, 255, 255, 0.62)',
    maxWidth: '68ch',
    lineHeight: '1.72',
  },
  releaseCard: {
    justifySelf: 'start',
    display: 'grid',
    rowGap: '8px',
    minWidth: '210px',
    ...shorthands.padding('16px'),
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.08)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    backgroundColor: 'rgba(255, 255, 255, 0.04)',
    '@media (min-width: 900px)': {
      justifySelf: 'end',
    },
  },
  releaseTitle: {
    color: 'rgba(255, 255, 255, 0.45)',
    letterSpacing: '0.05em',
    textTransform: 'uppercase' as const,
  },
  releaseValue: {
    color: '#ffffff',
    fontWeight: tokens.fontWeightSemibold,
    fontSize: '1.2rem',
  },
  buttonRow: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(230px, 1fr))',
    gap: '10px',
    width: '100%',
  },
  primaryButton: {
    width: '100%',
    backgroundColor: '#0078D4',
    color: '#ffffff',
    ...shorthands.border('1px', 'solid', '#0078D4'),
    fontWeight: tokens.fontWeightSemibold,
    ':hover': {
      backgroundColor: '#1a86e0',
      color: '#ffffff',
    },
    ':active': {
      backgroundColor: '#006cbf',
      color: '#ffffff',
    },
  },
  ghostButton: {
    width: '100%',
    backgroundColor: 'rgba(255, 255, 255, 0.055)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.1)'),
    color: 'rgba(255, 255, 255, 0.82)',
  },
  dropdownContainer: {
    position: 'relative',
    width: '100%',
  },
  dropdownButton: {
    width: '100%',
    backgroundColor: 'rgba(255, 255, 255, 0.055)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.1)'),
    color: 'rgba(255, 255, 255, 0.88)',
  },
  dropdownMenu: {
    position: 'absolute',
    top: 'calc(100% + 8px)',
    right: '0',
    width: 'min(92vw, 420px)',
    maxHeight: '320px',
    overflowY: 'auto',
    zIndex: '30',
    backgroundColor: 'rgba(24, 24, 24, 0.96)',
    backdropFilter: 'blur(20px)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.12)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ...shorthands.padding('8px'),
    boxShadow: '0 18px 44px rgba(0, 0, 0, 0.55)',
  },
  dropdownTitle: {
    color: 'rgba(255, 255, 255, 0.72)',
    ...shorthands.padding('8px', '10px', '6px', '10px'),
    letterSpacing: '0.01em',
  },
  dropdownItem: {
    display: 'grid',
    rowGap: '2px',
    ...shorthands.padding('10px'),
    ...shorthands.borderRadius(tokens.borderRadiusMedium),
    textDecorationLine: 'none',
    color: 'rgba(255, 255, 255, 0.9)',
    ':hover': {
      backgroundColor: 'rgba(255, 255, 255, 0.08)',
    },
  },
  dropdownItemMeta: {
    color: 'rgba(255, 255, 255, 0.52)',
  },
  dropdownList: {
    listStyleType: 'none',
    ...shorthands.padding(0),
    ...shorthands.margin(0),
  },
  statsGrid: {
    display: 'grid',
    gap: '12px',
    '@media (min-width: 860px)': {
      gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
    },
  },
  statCard: {
    backgroundColor: 'rgba(30, 30, 30, 0.78)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.07)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ...shorthands.padding('16px'),
    display: 'grid',
    rowGap: '6px',
    minHeight: '88px',
  },
  statCardSpanTwo: {
    '@media (min-width: 860px)': {
      gridColumn: 'span 2',
    },
  },
  statValue: {
    color: '#ffffff',
    fontFamily: '"Segoe UI Variable Display", "Space Grotesk", "Trebuchet MS", sans-serif',
    fontSize: '1.3rem',
    fontWeight: tokens.fontWeightSemibold,
  },
  statLabel: {
    color: 'rgba(255, 255, 255, 0.50)',
  },
  contentGrid: {
    display: 'grid',
    gap: '14px',
    '@media (min-width: 900px)': {
      gridTemplateColumns: 'repeat(3, minmax(0, 1fr))',
    },
  },
  card: {
    backgroundColor: 'rgba(30, 30, 30, 0.78)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.075)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ...shorthands.padding('20px'),
    display: 'grid',
    rowGap: '10px',
  },
  featureTitle: {
    color: 'rgba(255, 255, 255, 0.92)',
  },
  featureDescription: {
    color: 'rgba(255, 255, 255, 0.54)',
    lineHeight: '1.65',
  },
  screenshotContainer: {
    marginTop: '16px',
    maxWidth: '720px',
    marginInline: 'auto',
    backgroundColor: 'rgba(0, 0, 0, 0.2)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.08)'),
    ...shorthands.borderRadius(tokens.borderRadiusXLarge),
    overflow: 'hidden',
  },
  screenshotImage: {
    width: '100%',
    height: 'auto',
    display: 'block',
    transition: 'transform 0.6s cubic-bezier(0.16, 1, 0.3, 1)',
    ':hover': {
      transform: 'scale(1.01)',
    },
  },
  widgetsCard: {
    backgroundColor: 'rgba(28, 28, 28, 0.82)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.08)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ...shorthands.padding('24px'),
    display: 'grid',
    rowGap: '16px',
  },
  longFormSection: {
    backgroundColor: 'rgba(27, 27, 27, 0.8)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.08)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ...shorthands.padding('24px'),
    display: 'grid',
    rowGap: '14px',
  },
  sectionHeading: {
    marginTop: '0',
    marginBottom: '0',
    color: '#ffffff',
    letterSpacing: '-0.01em',
  },
  sectionLead: {
    marginTop: '0',
    marginBottom: '0',
    color: 'rgba(255, 255, 255, 0.66)',
    lineHeight: '1.75',
    maxWidth: '78ch',
  },
  bulletList: {
    marginTop: '0',
    marginBottom: '0',
    paddingLeft: '18px',
    display: 'grid',
    rowGap: '10px',
    color: 'rgba(255, 255, 255, 0.64)',
    lineHeight: '1.7',
  },
  communityNotice: {
    marginTop: '0',
    marginBottom: '0',
    color: 'rgba(255, 255, 255, 0.72)',
    lineHeight: '1.7',
  },
  communitySection: {
    backgroundColor: 'rgba(25, 25, 25, 0.82)',
    ...shorthands.border('1px', 'solid', 'rgba(96, 174, 255, 0.3)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ...shorthands.padding('24px'),
    display: 'grid',
    rowGap: '14px',
  },
  faqGrid: {
    display: 'grid',
    gap: '12px',
    '@media (min-width: 900px)': {
      gridTemplateColumns: 'repeat(2, minmax(0, 1fr))',
    },
  },
  faqCard: {
    backgroundColor: 'rgba(30, 30, 30, 0.78)',
    ...shorthands.border('1px', 'solid', 'rgba(255, 255, 255, 0.075)'),
    ...shorthands.borderRadius(tokens.borderRadiusLarge),
    ...shorthands.padding('18px'),
    display: 'grid',
    rowGap: '8px',
  },
  faqCardWide: {
    '@media (min-width: 900px)': {
      gridColumn: '1 / -1',
    },
  },
  faqQuestion: {
    marginTop: '0',
    marginBottom: '0',
    color: 'rgba(255, 255, 255, 0.92)',
    fontWeight: tokens.fontWeightSemibold,
  },
  faqAnswer: {
    marginTop: '0',
    marginBottom: '0',
    color: 'rgba(255, 255, 255, 0.62)',
    lineHeight: '1.7',
  },
  widgetList: {
    display: 'flex',
    flexWrap: 'wrap',
    gap: '8px',
  },
  footer: {
    color: 'rgba(255, 255, 255, 0.34)',
    textAlign: 'center',
    ...shorthands.padding('8px', '0', '0', '0'),
  },
})

function formatReleaseDate(isoDate: string): string {
  const date = new Date(isoDate)
  if (Number.isNaN(date.getTime())) {
    return 'Unknown release date'
  }

  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
  }).format(date)
}

function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) {
    return 'Unknown size'
  }

  if (bytes < 1024) {
    return `${bytes} B`
  }

  const units = ['KB', 'MB', 'GB']
  let value = bytes / 1024
  let unitIndex = 0

  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024
    unitIndex += 1
  }

  return `${value.toFixed(value >= 100 ? 0 : 1)} ${units[unitIndex]}`
}

function readCachedRelease(): GitHubRelease | null {
  if (typeof window === 'undefined') {
    return null
  }

  try {
    const raw = window.localStorage.getItem(RELEASE_CACHE_KEY)
    if (!raw) {
      return null
    }

    const cached = JSON.parse(raw) as CachedReleaseRecord
    if (!cached?.savedAt || !cached?.release) {
      return null
    }

    if (Date.now() - cached.savedAt > RELEASE_CACHE_TTL_MS) {
      window.localStorage.removeItem(RELEASE_CACHE_KEY)
      return null
    }

    return cached.release
  } catch {
    return null
  }
}

function writeCachedRelease(release: GitHubRelease): void {
  if (typeof window === 'undefined') {
    return
  }

  try {
    const record: CachedReleaseRecord = {
      savedAt: Date.now(),
      release,
    }

    window.localStorage.setItem(RELEASE_CACHE_KEY, JSON.stringify(record))
  } catch {
    // Ignore storage errors to avoid blocking UI rendering.
  }
}

function App() {
  const styles = useStyles()
  const [latestRelease, setLatestRelease] = useState<GitHubRelease | null>(null)
  const [isLoadingRelease, setIsLoadingRelease] = useState(true)
  const [isDownloadMenuOpen, setIsDownloadMenuOpen] = useState(false)
  const downloadMenuContainerRef = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    const abortController = new AbortController()
    const cachedRelease = readCachedRelease()

    if (cachedRelease) {
      setLatestRelease(cachedRelease)
      setIsLoadingRelease(false)
    }

    async function loadLatestRelease(): Promise<void> {
      try {
        const response = await fetch(LATEST_RELEASE_API_URL, {
          signal: abortController.signal,
          headers: {
            Accept: 'application/vnd.github+json',
          },
        })

        if (!response.ok) {
          throw new Error(`GitHub API failed with status ${response.status}`)
        }

        const payload = (await response.json()) as GitHubRelease
        setLatestRelease(payload)
        writeCachedRelease(payload)
      } catch {
        if (!cachedRelease) {
          setLatestRelease(null)
        }
      } finally {
        setIsLoadingRelease(false)
      }
    }

    const fetchTimer = window.setTimeout(() => {
      void loadLatestRelease()
    }, cachedRelease ? 1200 : 350)

    return () => {
      window.clearTimeout(fetchTimer)
      abortController.abort()
    }
  }, [])

  useEffect(() => {
    if (!isDownloadMenuOpen) {
      return
    }

    function handleDocumentPointerDown(event: PointerEvent): void {
      const target = event.target
      if (!(target instanceof Node)) {
        return
      }

      if (!downloadMenuContainerRef.current?.contains(target)) {
        setIsDownloadMenuOpen(false)
      }
    }

    window.addEventListener('pointerdown', handleDocumentPointerDown)

    return () => {
      window.removeEventListener('pointerdown', handleDocumentPointerDown)
    }
  }, [isDownloadMenuOpen])

  const releaseText = useMemo(() => {
    if (isLoadingRelease) {
      return 'Loading latest release...'
    }

    if (!latestRelease?.tag_name) {
      return 'Release info unavailable'
    }

    return latestRelease.tag_name
  }, [isLoadingRelease, latestRelease])

  const releaseDate = useMemo(() => {
    if (!latestRelease?.published_at) {
      return 'Check release feed for updates'
    }

    return `Published ${formatReleaseDate(latestRelease.published_at)}`
  }, [latestRelease])

  const latestAssets = useMemo(() => {
    if (!latestRelease?.assets?.length) {
      return []
    }

    return latestRelease.assets.filter((asset) => Boolean(asset.browser_download_url))
  }, [latestRelease])

  const widgets = [
    'clock',
    'weather',
    'network-tools',
    'process-manager',
    'clipboard-studio',
    'file-explorer',
    'dns-lookup',
    'powershell-console',
  ]

  const highlights = [
    { value: '20+', label: 'Built-in templates' },
    { value: 'Hot Reload', label: 'Live widget preview while editing' },
    { value: 'WebView2', label: 'Native rendering stack' },
    { value: 'Open Source', label: 'GPL 3.0 licensed project' },
    { value: 'Built-in Editor', label: 'Create and edit widgets in-app', spanTwo: true },
    { value: 'PowerShell API', label: 'Host-side automation bridge', spanTwo: true },
  ]

  const workflowUseCases = [
    'Create a always-on desktop clock, weather panel, and quick actions bar for daily focus sessions.',
    'Run network checks, DNS lookups, and process monitoring widgets during development or support tasks.',
    'Use HTWind PowerShell integration for controlled automation while keeping execution consent explicit.',
    'Build custom internal widgets with HTML, CSS, and JavaScript to expose team-specific operational tools.',
  ]

  const communitySharingSteps = [
    'Open the HTWind GitHub Discussions area and create a new discussion in the widget-sharing category.',
    'Add a short widget summary, screenshots, and the main problem your widget solves on Windows.',
    'Share setup notes, expected permissions, and any host API or PowerShell usage details for safe adoption.',
    'Update your discussion when you publish improvements so other users can track versions and feedback.',
  ]

  const faqItems = [
    {
      question: 'What is HTWind used for?',
      answer:
        'HTWind is used to manage and display HTML-based desktop widgets on Windows. It helps users keep diagnostics, productivity shortcuts, and system insights visible on top of regular applications.',
    },
    {
      question: 'Can I build my own widgets?',
      answer:
        'Yes. You can create custom widgets with web technologies including HTML, CSS, and JavaScript. HTWind renders widgets through WebView2 and provides host APIs for desktop integration scenarios.',
    },
    {
      question: 'Does HTWind support PowerShell automation?',
      answer:
        'Yes. HTWind includes host-side APIs that can execute approved PowerShell commands, enabling practical automation workflows for diagnostics and controlled desktop actions.',
    },
    {
      question: 'Is HTWind open source?',
      answer:
        'HTWind is an open-source project published on GitHub under the GPL 3.0 license, making it suitable for personal use, experimentation, and community-driven improvements.',
    },
    {
      question: 'Are contributions welcome?',
      answer:
        'Yes. Contributions are welcome. You can open issues, share ideas in Discussions, and submit pull requests to improve widgets, templates, documentation, and core app features.',
      spanTwoColumns: true,
    },
  ]

  return (
    <FluentProvider theme={webDarkTheme} className={styles.page}>
      <main className={styles.shell}>

        <div className={styles.banner}>
          <Desktop24Regular />
          <Caption1>Windows widget platform with native integration and web-level flexibility</Caption1>
        </div>

        <Card className={styles.heroCard}>
          <div className={styles.heroTopRow}>
            <div>
              <h1 className={styles.heroHeading}>HTWind for Windows 11 desktops</h1>
              <p className={styles.heroDescription}>
                HTWind is a customizable desktop widget manager that lets you run rich HTML widgets,
                monitor your system, and execute PowerShell tools from a polished Windows-focused workspace.
              </p>
            </div>

            <aside className={styles.releaseCard} aria-live="polite">
              <Caption1 className={styles.releaseTitle}>Latest release</Caption1>
              <span className={styles.releaseValue}>
                {isLoadingRelease ? <Spinner size="tiny" labelPosition="after" label="Loading" /> : releaseText}
              </span>
              <Caption1>{releaseDate}</Caption1>
                <div style={{ marginTop: '10px' }}>
                  <a
                    href="https://apps.microsoft.com/detail/9PN58CG1P20L?referrer=appbadge&cid=sametcn99&mode=full"
                    target="_blank"
                    rel="noopener noreferrer"
                    aria-label="Get it from Microsoft Store"
                  >
                    <img
                      src="https://get.microsoft.com/images/en-us%20dark.svg"
                      width="160"
                      alt="Get it from Microsoft Store"
                      style={{ display: 'block' }}
                    />
                  </a>
                </div>
            </aside>
          </div>

          <div className={styles.buttonRow}>
            <Button
              as="a"
              href={GITHUB_REPOSITORY_URL}
              target="_blank"
              rel="noreferrer"
              icon={<Code24Regular />}
              className={styles.primaryButton}
              size="large"
            >
              View on GitHub
            </Button>

            {/* Browse Releases removed per request; Microsoft Store badge placed at row end */}

            <Button
              as="a"
              href={latestRelease?.html_url ?? RELEASES_URL}
              target="_blank"
              rel="noreferrer"
              icon={<Open24Regular />}
              className={styles.ghostButton}
              size="large"
            >
              Open Latest Release
            </Button>

            <div ref={downloadMenuContainerRef} className={styles.dropdownContainer}>
              <Button
                icon={<ArrowDownload24Regular />}
                className={styles.dropdownButton}
                size="large"
                disabled={isLoadingRelease || latestAssets.length === 0}
                onClick={() => {
                  setIsDownloadMenuOpen((value) => !value)
                }}
                aria-expanded={isDownloadMenuOpen}
                aria-haspopup="menu"
              >
                {isLoadingRelease
                  ? 'Loading assets...'
                  : latestAssets.length === 0
                    ? 'No downloadable assets'
                    : `Download Latest (${latestAssets.length})`}
              </Button>

              {isDownloadMenuOpen && latestAssets.length > 0 && (
                <div className={styles.dropdownMenu}>
                  <Caption1 className={styles.dropdownTitle}>Latest Release Assets</Caption1>
                  <ul className={styles.dropdownList} aria-label="Latest release asset downloads">
                    {latestAssets.map((asset) => (
                      <li key={asset.id}>
                        <Link
                          href={asset.browser_download_url}
                          target="_blank"
                          rel="noreferrer"
                          className={styles.dropdownItem}
                          onClick={() => {
                            setIsDownloadMenuOpen(false)
                          }}
                        >
                          <span>{asset.name}</span>
                          <Caption1 className={styles.dropdownItemMeta}>{formatFileSize(asset.size)}</Caption1>
                        </Link>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </div>


          </div>

          <Body1 className={styles.communityNotice}>
            You can share your custom widgets with the community in{' '}
            <Link href={GITHUB_DISCUSSIONS_URL} target="_blank" rel="noreferrer">
              GitHub Discussions
            </Link>
            , discover ready-to-use examples, and get feedback from other HTWind users.
          </Body1>
        </Card>

        <section className={styles.statsGrid} aria-label="Project highlights">
          {highlights.map((item) => (
            <Card
              key={item.label}
              className={mergeClasses(styles.statCard, item.spanTwo && styles.statCardSpanTwo)}
            >
              <span className={styles.statValue}>{item.value}</span>
              <Caption1 className={styles.statLabel}>{item.label}</Caption1>
            </Card>
          ))}
        </section>

        <section className={styles.contentGrid}>
          <Card className={styles.card}>
            <WindowWrench24Regular fontSize={24} />
            <Subtitle1 className={styles.featureTitle}>HTML Widgets on Desktop</Subtitle1>
            <Body1 className={styles.featureDescription}>
              Build and run widgets with HTML, CSS, and JavaScript. Keep your tools always visible and fully
              customizable on your desktop.
            </Body1>
          </Card>

          <Card className={styles.card}>
            <Rocket24Regular fontSize={24} />
            <Subtitle1 className={styles.featureTitle}>PowerShell Integration</Subtitle1>
            <Body1 className={styles.featureDescription}>
              Trigger safe, explicit PowerShell scripts through HTWind host APIs for quick diagnostics,
              automation, and system actions.
            </Body1>
          </Card>

          <Card className={styles.card}>
            <Desktop24Regular fontSize={24} />
            <Subtitle1 className={styles.featureTitle}>Windows 11 Native Feel</Subtitle1>
            <Body1 className={styles.featureDescription}>
              Designed for modern Windows workflows with tray behavior, pin-on-top controls, and state persistence
              tuned for desktop productivity.
            </Body1>
          </Card>
        </section>

        <section>
          <Card className={styles.widgetsCard}>
            <Title1>Built-in widgets</Title1>
            <Body1 className={styles.featureDescription}>
              HTWind ships with practical templates for system insight, media controls, file operations, and quick
              actions.
            </Body1>
            <div className={styles.widgetList}>
              {widgets.map((widget) => (
                <Badge key={widget} size="large" appearance="tint" color="informative">
                  {widget}
                </Badge>
              ))}
            </div>
            <Body1>
              For complete details, check the{' '}
              <Link href={`${GITHUB_REPOSITORY_URL}#built-in-widgets`} target="_blank" rel="noreferrer">
                repository documentation
              </Link>
              .
            </Body1>
          </Card>
        </section>

        <section aria-labelledby="htwind-overview-heading">
          <Card className={styles.longFormSection}>
            <h2 id="htwind-overview-heading" className={styles.sectionHeading}>
              Desktop widget manager for Windows productivity
            </h2>
            <p className={styles.sectionLead}>
              HTWind is a Windows desktop widget manager that combines native app behavior with flexible web content.
              By using HTML widgets and WebView2 rendering, the platform makes it easier to build desktop tools that
              stay accessible while you work. Typical setups include clock and calendar utilities, system monitoring
              dashboards, and quick command launchers that remain visible across multi-window workflows.
            </p>
            <p className={styles.sectionLead}>
              The project is designed for users who want a lightweight but extensible desktop customization layer.
              With tray integration, pin-on-top window controls, state persistence, and template-based widgets,
              HTWind helps turn a standard Windows 11 workspace into a more actionable and information-rich
              environment without requiring heavy desktop shell replacements.
            </p>

            <div className={styles.screenshotContainer}>
              <img
                src="/screenshot.png"
                alt="HTWind Desktop App Screenshot - High-quality widgets on Windows 11"
                className={styles.screenshotImage}
                loading="lazy"
                decoding="async"
              />
            </div>
          </Card>
        </section>

        <section aria-labelledby="htwind-use-cases-heading">
          <Card className={styles.longFormSection}>
            <h2 id="htwind-use-cases-heading" className={styles.sectionHeading}>
              Common HTWind use cases
            </h2>
            <p className={styles.sectionLead}>
              The following workflows highlight how HTWind can be used as a practical Windows 11 widget platform in
              both personal productivity and technical operations contexts.
            </p>
            <ul className={styles.bulletList}>
              {workflowUseCases.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </Card>
        </section>

        <section aria-labelledby="htwind-faq-heading">
          <Card className={styles.longFormSection}>
            <h2 id="htwind-faq-heading" className={styles.sectionHeading}>
              HTWind FAQ
            </h2>
            <p className={styles.sectionLead}>
              Quick answers for users searching for a Windows HTML widget manager with PowerShell integration and
              open-source customization.
            </p>
            <div className={styles.faqGrid}>
              {faqItems.map((faq) => (
                <article
                  key={faq.question}
                  className={`${styles.faqCard} ${faq.spanTwoColumns ? styles.faqCardWide : ''}`}
                >
                  <h3 className={styles.faqQuestion}>{faq.question}</h3>
                  <p className={styles.faqAnswer}>{faq.answer}</p>
                </article>
              ))}
            </div>
          </Card>
        </section>

        <section aria-labelledby="htwind-community-sharing-heading">
          <Card className={styles.communitySection}>
            <h2 id="htwind-community-sharing-heading" className={styles.sectionHeading}>
              Share your widgets in HTWind GitHub Discussions
            </h2>
            <p className={styles.sectionLead}>
              HTWind supports a community-driven workflow where users publish and discuss widget ideas directly in
              GitHub Discussions. This makes it easy to exchange desktop widget templates, compare approaches for
              Windows automation, and improve widget quality through real usage feedback.
            </p>
            <p className={styles.sectionLead}>
              If you build a clock variation, a system monitor, a file utility panel, or any custom HTML widget,
              you can post it in Discussions so others can test and adapt it. Include screenshots, usage notes,
              and integration details to help users quickly adopt your widget in their own HTWind setup.
            </p>
            <ul className={styles.bulletList}>
              {communitySharingSteps.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
            <Body1>
              Visit the{' '}
              <Link href={GITHUB_DISCUSSIONS_URL} target="_blank" rel="noreferrer">
                HTWind Discussions board
              </Link>{' '}
              to publish your widgets and collaborate with the community.
            </Body1>
          </Card>
        </section>

        <section aria-labelledby="support-developer-heading">
          <Card className={styles.longFormSection}>
            <h2 id="support-developer-heading" className={styles.sectionHeading}>
              Support the developer
            </h2>
            <p className={styles.sectionLead}>
              You can support sustainable development of HTWind and help the project continue to grow by visiting
              the support page.
            </p>
            <Body1>
              Support link:{' '}
              <Link href={SUPPORT_URL} target="_blank" rel="noreferrer">
                sametcc.me/support
              </Link>
            </Body1>
          </Card>
        </section>

        <footer className={styles.footer}>
          <Caption1>HTWind | Open-source desktop widget manager for Windows 11</Caption1>
        </footer>
      </main>
    </FluentProvider>
  )
}

export default App
