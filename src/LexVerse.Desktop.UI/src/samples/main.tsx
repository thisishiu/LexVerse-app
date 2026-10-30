import { useLayoutEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { apps, blocksFor, gameChrome, type AppId, type TextBlock } from "./content";
import { SampleRenderer } from "./renderer";
import "./samples.css";

const parameter = new URLSearchParams(location.search).get("app");
const appId: AppId = parameter && Object.hasOwn(apps, parameter) ? parameter as AppId : "article";
const app = apps[appId];
const renderer = new SampleRenderer(appId);
window.sampleCommand = renderer.command;
document.title = app.name;
document.documentElement.lang = app.sourceLanguage;
document.body.className = `app-${appId}`;

function Text({ block, className = "" }: { block: TextBlock; className?: string }) {
  return <div className={`source-block ${className}`} data-block={block.id}><span className="source-text">{block.source}</span></div>;
}

function ContentApp() {
  const [page, setPage] = useState(0);
  const [zoom, setZoom] = useState(1);
  const [saved, setSaved] = useState(false);
  const [search, setSearch] = useState("");
  const blocks = blocksFor(appId, page);
  useLayoutEffect(() => { renderer.pageChanged(); document.querySelector("main")?.scrollTo(0, 0); }, [page]);
  useLayoutEffect(() => { if (appId === "game") renderer.command({ request: { operation: "refresh" } }); }, [saved]);
  const choose = (value: number) => { setPage(value); setSaved(false); };
  if (appId === "article") return <div className="reader-shell">
    <header className="reader-header"><span className="fieldnotes-logo">f<span>FIELDNOTES</span></span><span className="header-divider"/><span className="reader-subtitle">A place for good ideas</span><button className="reader-save" onClick={() => setSaved(value => !value)}>{saved ? "✓ Saved to library" : "+ Save article"}</button></header>
    <aside className="reader-sidebar"><span className="sidebar-label">YOUR READING ROOM</span><h2>Library</h2><input aria-label="Search articles" placeholder="Search articles…" value={search} onChange={event => setSearch(event.target.value)}/><span className="sidebar-label">COLLECTION / EVERYDAY IDEAS</span>{app.pages.map((item, i) => item.label.toLowerCase().includes(search.toLowerCase()) && <button key={item.label} className={page === i ? "active" : ""} onClick={() => choose(i)}><span>0{i + 1}</span>{item.label}<small>{i === 0 ? "Design · 5 min read" : "Perspective · 4 min read"}</small></button>)}<div className="library-note">Read slowly.<br/>Notice more.</div></aside>
    <main className="article-scroll"><div className="article-toolbar"><span>ESSAYS / {page === 0 ? "DESIGN & TECHNOLOGY" : "PERSPECTIVE"}</span><div><button aria-label="Decrease text size" onClick={() => setZoom(value => Math.max(.85, value - .1))}>A−</button><button aria-label="Increase text size" onClick={() => setZoom(value => Math.min(1.2, value + .1))}>A+</button></div></div><article style={{ fontSize: `${zoom}em` }}><span className="article-kicker">THE EVERYDAY SERIES · NO. {page + 1}</span><Text block={blocks[0]} className="article-title"/><div className="byline"><span className="avatar">EK</span><span>Eleanor Kim<small>October 18 · {page === 0 ? "5" : "4"} min read</small></span></div><div className="article-art"><span>{page === 0 ? <>Less friction.<br/>More wonder.</> : <>Look closer.<br/>Stay curious.</>}</span><div className="art-circle"/></div>{blocks.slice(1).map((block, index) => <Text key={block.id} block={block} className={index === 0 ? "article-lede" : "article-paragraph"}/>)}<hr/><p className="article-end">Thank you for reading. Make a little room for your next idea.</p><button className="next-article" onClick={() => choose(1 - page)}>Read next: {app.pages[1 - page].label} →</button></article></main>
  </div>;
  if (appId === "game") return <div className="game-shell">
    <div className="game-world"><div className="game-moon"/><div className="ridge ridge-back"/><div className="ridge ridge-front"/><div className="lighthouse"><i/></div><div className="sea"/></div>
    <header className="game-header"><span className="starfall-logo">✦ STARFALL</span><Text block={gameChrome.header} className="game-ui game-header-log"/><div className="player-avatar">L</div></header>
    <aside className="quest-list"><span>QUEST JOURNAL</span><Text block={gameChrome.journal} className="game-ui quest-journal-title"/>{app.pages.map((item, i) => <button key={item.label} className={page === i ? "active" : ""} onClick={() => choose(i)}><small>CHAPTER 0{i + 1}</small><Text block={i === 0 ? gameChrome.menuOne : gameChrome.menuTwo} className="game-ui quest-menu-text"/></button>)}</aside>
    <main className="quest-scroll"><article className="quest-card"><div className="quest-number">✧ MAIN QUEST · 0{page + 1}</div><Text block={blocks[0]} className="quest-title"/><div className="quest-rule"/><Text block={gameChrome.objective} className="game-ui quest-label quest-objective"/><Text block={blocks[1]} className="quest-goal"/><Text block={blocks[2]} className="quest-detail"/><div className="quest-rewards"><span className="quest-label">REWARDS</span><Text block={blocks[3]}/></div><button className="quest-track" onClick={() => setSaved(value => !value)}><Text block={saved ? gameChrome.tracking : gameChrome.track} className="game-ui quest-action-text"/></button><button className="quest-next" onClick={() => choose(1 - page)}><Text block={page === 0 ? gameChrome.next : gameChrome.previous} className="game-ui quest-action-text"/></button></article></main>
    <footer className="player-hud"><span>LYRA <small>LV. 12</small></span><div className="health">HP <b>1,280 / 1,280</b></div><div className="mana">MP <b>360 / 360</b></div><span className="world-clock">STARFALL VALLEY · 04:38</span></footer>
  </div>;
  if (appId === "letters") return <div className="mail-shell"><header className="mail-header"><span className="postbox-logo">▰ <b>Postbox</b></span><span className="mail-account">Jiyoon Park <i>JP</i></span></header><aside className="mail-folders"><button className="compose" onClick={() => setSaved(value => !value)}>{saved ? "✓ 임시 보관함에 저장됨" : "+ 새 메모"}</button><h3>편지함</h3><div className="folder-current">받은 편지함 <b>2</b></div><div>별표 표시</div><div>보낸 편지함</div><div>보관함</div><span className="mail-storage">240 MB of 5 GB used<div/></span></aside><section className="mail-list"><h2>받은 편지함 <small>2</small></h2>{app.pages.map((item, i) => <button key={item.label} className={page === i ? "active" : ""} onClick={() => choose(i)}><span className="sender-avatar">{i === 0 ? "민" : "서"}</span><div><strong>{i === 0 ? "민서" : "서연"}</strong><span>{item.label}</span><small>{i === 0 ? "천천히 가도 괜찮아요…" : "토요일 오후에 강변을…"}</small></div><time>{i === 0 ? "09:41" : "어제"}</time></button>)}</section><main className="letter-scroll"><div className="letter-toolbar"><span>↩ &nbsp; ↪</span><button onClick={() => setSaved(value => !value)} aria-label="Star message">{saved ? "★" : "☆"}</button></div><article className="letter-body"><Text block={blocks[0]} className="letter-title"/><div className="letter-sender"><span className="sender-avatar">{page === 0 ? "민" : "서"}</span><div><b>{page === 0 ? "민서" : "서연"}</b><small>받는 사람: 나</small></div><time>10월 18일</time></div><p className="letter-greeting">지윤님, 안녕하세요.</p>{blocks.slice(1).map(block => <Text key={block.id} block={block} className="letter-paragraph"/>)}<p className="letter-signature">따뜻한 마음을 담아,<br/>{page === 0 ? "민서" : "서연"}</p></article></main></div>;
  return <div className="comic-shell"><header className="comic-header"><span className="afterrain-logo">◒ <b>After Rain</b></span><span className="comic-series">雨后的约定 <small>第一章</small></span><button onClick={() => setSaved(value => !value)}>{saved ? "♥ 已收藏" : "♡ 收藏"}</button></header><main className="comic-scroll"><div className="comic-page" key={page}><div className={`comic-panel panel-one page-${page}`}><div className="comic-star">✦</div><div className="comic-person person-one"/><div className="comic-hill"/><Text block={blocks[0]} className="speech-bubble bubble-one"/><span className="panel-caption">{page === 0 ? "傍晚，山坡上。" : "空气里有雨水的味道。"}</span></div><div className={`comic-panel panel-two page-${page}`}><div className="comic-star star-two">✧</div><div className="comic-person person-two"/><div className="comic-person person-three"/><div className="comic-hill"/><Text block={blocks[1]} className="speech-bubble bubble-two"/></div></div></main><footer className="comic-navigation"><button disabled={page === 0} onClick={() => choose(page - 1)}>← 上一页</button><span>0{page + 1} <i>/ 02</i></span><button disabled={page === 1} onClick={() => choose(page + 1)}>下一页 →</button></footer></div>;
}

createRoot(document.getElementById("root")!).render(<ContentApp/>);
