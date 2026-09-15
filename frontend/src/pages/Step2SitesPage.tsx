import { useMemo, useState } from "react";
import { useWizard } from "../state/WizardContext";
import { validateDomain } from "./validateDomain";
import "./Step2SitesPage.css";

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

  return (
    <div className="step2-page">
      <div className="step2-main">
        <h1 className="page-title">步驟 2：設定允許孩子訪問的網站</h1>
        <p className="page-description">
          除了以下列出的網址外，瀏覽器將自動封鎖所有其他網路訪問。子網域視為不同項目，例如
          www.example.com 和 example.com 需分別加入。
        </p>

        <div className="site-input-row">
          <input
            className="text-input"
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
              <span className="site-domain">{site.domain}</span>
              {site.categoryLabel && <span className="pill pill-info">{site.categoryLabel}</span>}
              <button className="btn btn-destructive" onClick={() => removeSite(site.domain)}>刪除</button>
            </div>
          ))}
        </div>
      </div>

      <aside className="step2-sidebar">
        <h2 className="section-title">白名單分類分佈</h2>
        {categoryBreakdown.map((item) => (
          <div key={item.label} className="breakdown-row">
            {item.label} — {item.count} 個 ({item.percentage}%)
          </div>
        ))}
      </aside>
    </div>
  );
}
