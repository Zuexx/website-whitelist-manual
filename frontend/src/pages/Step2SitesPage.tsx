import { useMemo, useState } from "react";
import { Info, Lock, PieChart, Trash2 } from "lucide-react";
import { useWizard } from "../state/WizardContext";
import { validateDomain } from "./validateDomain";
import "./Step2SitesPage.css";

const CHART_COLORS = ["#0f766e", "#10b981", "#3b82f6", "#d97706", "#6366f1", "#ec4899"];

export function Step2SitesPage() {
  const { allowlistSites, setAllowlistSites } = useWizard();
  const [domainInput, setDomainInput] = useState("");
  const [categoryInput, setCategoryInput] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);

  function addSite() {
    const result = validateDomain(domainInput);
    if (!result.ok) {
      setValidationError(result.error);
      return;
    }
    setValidationError(null);
    setAllowlistSites([
      ...allowlistSites,
      { domain: domainInput.trim(), categoryLabel: categoryInput.trim() || null },
    ]);
    setDomainInput("");
    setCategoryInput("");
  }

  function removeSite(domain: string) {
    setAllowlistSites(allowlistSites.filter((site) => site.domain !== domain));
  }

  const categoryBreakdown = useMemo(() => {
    const total = allowlistSites.length;
    if (total === 0) return [];
    const counts = new Map<string, number>();
    for (const site of allowlistSites) {
      const label = site.categoryLabel ?? "未分類";
      counts.set(label, (counts.get(label) ?? 0) + 1);
    }
    return Array.from(counts.entries())
      .map(([label, count]) => ({ label, count, percentage: Math.round((100 * count) / total) }))
      .sort((a, b) => b.count - a.count);
  }, [allowlistSites]);

  const donutGradient = useMemo(() => {
    if (categoryBreakdown.length === 0) return "conic-gradient(var(--color-border) 0deg 360deg)";
    let cursor = 0;
    const stops = categoryBreakdown.map((item, index) => {
      const color = CHART_COLORS[index % CHART_COLORS.length];
      const start = (cursor / 100) * 360;
      cursor += item.percentage;
      const end = (cursor / 100) * 360;
      return `${color} ${start}deg ${end}deg`;
    });
    return `conic-gradient(${stops.join(", ")})`;
  }, [categoryBreakdown]);

  return (
    <div className="step2-page">
      <div className="step2-main">
        <h1 className="page-title">步驟 2：設定允許孩子訪問的網站</h1>
        <p className="page-description">
          除了以下列出的網址外，瀏覽器將自動封鎖所有其他網路訪問。子網域視為不同項目，例如
          www.example.com 和 example.com 需分別加入。
        </p>

        <div className="card info-banner banner-row">
          <span className="icon-chip icon-chip-sm icon-chip-info"><Info /></span>
          <div className="banner-row-text">
            <strong>子網域將視為不同項目</strong>
            <p>若學校或特定平台使用子網域，請將完整前綴一併加入白名單。</p>
          </div>
        </div>

        <div className="site-input-row">
          <span className="site-input-prefix"><Lock /></span>
          <input
            className="text-input site-input-domain"
            placeholder="輸入網域，例如 classroom.google.com"
            value={domainInput}
            onChange={(event) => setDomainInput(event.target.value)}
          />
          <input
            className="text-input text-input-narrow"
            placeholder="分類標籤 (選填)"
            value={categoryInput}
            onChange={(event) => setCategoryInput(event.target.value)}
          />
          <button className="btn btn-primary" onClick={addSite}>新增至白名單</button>
        </div>
        {validationError && <p className="validation-error">{validationError}</p>}

        <h2 className="section-title">目前已允許的網站清單 (共 {allowlistSites.length} 個網址)</h2>
        <div className="site-list">
          {allowlistSites.map((site) => (
            <div key={site.domain} className="card site-row">
              <span className="icon-chip icon-chip-sm icon-chip-success"><Lock /></span>
              <span className="site-domain">{site.domain}</span>
              {site.categoryLabel && <span className="pill pill-info">{site.categoryLabel}</span>}
              <button className="btn btn-destructive" onClick={() => removeSite(site.domain)}>
                <Trash2 /> 刪除
              </button>
            </div>
          ))}
          {allowlistSites.length === 0 && <p className="site-list-empty">尚未加入任何網站，孩子將無法瀏覽任何網頁。</p>}
        </div>
      </div>

      <aside className="step2-sidebar">
        <div className="card">
          <h2 className="section-title-inline"><PieChart /> 白名單分類分佈</h2>
          {categoryBreakdown.length > 0 ? (
            <>
              <div className="donut-wrap">
                <div className="donut" style={{ background: donutGradient }}>
                  <div className="donut-hole">
                    <span className="donut-hole-value">{allowlistSites.length}</span>
                    <span className="donut-hole-label">總允許網址</span>
                  </div>
                </div>
              </div>
              <div className="breakdown-legend">
                {categoryBreakdown.map((item, index) => (
                  <div key={item.label} className="breakdown-row">
                    <span
                      className="breakdown-dot"
                      style={{ background: CHART_COLORS[index % CHART_COLORS.length] }}
                    />
                    <span className="breakdown-label">{item.label}</span>
                    <span className="breakdown-count">{item.count} 個 ({item.percentage}%)</span>
                  </div>
                ))}
              </div>
            </>
          ) : (
            <p className="breakdown-empty">加入網站後這裡會顯示分類比例。</p>
          )}
        </div>
      </aside>
    </div>
  );
}
