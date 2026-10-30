import { fileURLToPath } from 'node:url';
import { mkdir } from 'node:fs/promises';
import { spawn, execFile } from 'node:child_process';
import { promisify } from 'node:util';
import net from 'node:net';
import { randomUUID } from 'node:crypto';

const assert = (ok, message) => { if (!ok) throw new Error(message); };
const native = process.argv.includes('--native');
const baseUrl = process.env.SAMPLE_URL || 'http://127.0.0.1:5186';
const cases = ['article', 'game', 'letters', 'comic'];

if (native) {
  // Native integration smoke: independently hosted WebView2 + named-pipe transport.
  const executable = fileURLToPath(new URL('../src/LexVerse.App/bin/Debug/net10.0-windows10.0.19041.0/LexVerse.App.exe', import.meta.url));
  for (const app of cases) {
    const pipe = `sample-qa-${randomUUID()}`;
    const child = spawn(executable, ['--sample-app', '--case', app, '--pipe', pipe, '--parent', String(process.pid)], { windowsHide: true, stdio: 'ignore' });
    let spawnError;
    child.on('error', error => { spawnError = error; });
    const send = request => new Promise((resolve, reject) => {
      const socket = net.createConnection(`\\\\.\\pipe\\${pipe}`);
      let data = '';
      socket.setTimeout(5000, () => socket.destroy(new Error('Native request timeout')));
      socket.on('connect', () => socket.write(JSON.stringify(request) + '\n'));
      socket.on('error', reject);
      socket.on('data', bytes => {
        data += bytes.toString('utf8');
        if (data.includes('\n')) { socket.end(); resolve(JSON.parse(data.split('\n')[0])); }
      });
    });
    const command = async request => {
      for (let retry = 0; ; retry++) {
        try { return await send(request); }
        catch (error) {
          if (!['ENOENT', 'EBUSY'].includes(error.code) || retry >= 20) throw error;
          await new Promise(resolve => setTimeout(resolve, 100));
        }
      }
    };
    try {
      let ready = false;
      for (let attempt = 0; attempt < 40; attempt++) {
        if (spawnError) throw spawnError;
        if (child.exitCode !== null) throw new Error(`${app} exited before becoming ready (${child.exitCode})`);
        try { ready = (await command({ operation: 'ping' })).ok; if (ready) break; } catch {}
        await new Promise(resolve => setTimeout(resolve, 500));
      }
      assert(ready, `${app}: native WebView/pipe did not become ready`);
      const started = await command({ operation: 'start', kind: 'window', style: { dark: true, opacity: .96 } });
      assert(started.ok && started.count > 0, `${app}: native translation failed ${JSON.stringify(started)}`);
      assert((await command({ operation: 'stop' })).ok, `${app}: native stop failed`);
      assert((await command({ operation: 'clear' })).ok, `${app}: native clear failed`);
      console.log(`Native ${app}: startup, IPC, WebView handler, start/stop/clear passed.`);
    } finally { child.kill(); }
  }
  const host = spawn(executable, ['--demo', '--case', 'article'], { windowsHide: true, stdio: 'ignore' });
  const run = promisify(execFile);
  try {
    let launched = false;
    for (let attempt = 0; attempt < 20; attempt++) {
      assert(host.exitCode === null, 'Original workspace exited during recording startup');
      const { stdout } = await run('powershell.exe', ['-NoProfile', '-Command',
        `(Get-CimInstance Win32_Process -Filter 'ParentProcessId=${host.pid}' | Where-Object { $_.CommandLine -like '*--sample-app*' } | Measure-Object).Count`], { windowsHide: true });
      if (Number(stdout.trim()) > 0) { launched = true; break; }
      await new Promise(resolve => setTimeout(resolve, 500));
    }
    assert(launched, 'Original workspace did not launch its independent content process');
    console.log('Native workspace: original MainWindow startup and independent content process passed.');
  } finally { host.kill(); }
} else {
  const moduleUrl = process.env.PLAYWRIGHT_MODULE || new URL('../../.ui-qa/node_modules/playwright/index.mjs', import.meta.url).href;
  const { chromium } = await import(moduleUrl);
  const browser = await chromium.launch({ executablePath: process.env.CHROME_PATH || 'C:/Program Files/Google/Chrome/Application/chrome.exe', headless: true });
  await mkdir('.tmp/sample-qa', { recursive: true });
  const errors = [];
  try {
    for (const deviceScaleFactor of [1, 1.5]) {
      const context = await browser.newContext({ viewport: { width: 1240, height: 820 }, deviceScaleFactor });
      const page = await context.newPage();
      const external = [];
      page.on('pageerror', error => errors.push(error.message));
      page.on('request', request => { if (!request.url().startsWith(baseUrl)) external.push(request.url()); });
      const command = request => page.evaluate(request => window.sampleCommand({ request }), request);
      for (const app of cases) {
        await page.goto(`${baseUrl}/sample.html?app=${app}`);
        await page.evaluate(() => document.fonts.ready);
        assert(!/lexverse|demo/i.test(await page.locator('body').innerText()), `${app}: visible branding`);
        assert(!/lexverse|demo/i.test(await page.title()), `${app}: branded title`);
        await page.screenshot({ path: `.tmp/sample-qa/${app}-source-${deviceScaleFactor}.png` });
        if (app === 'letters') {
          assert((await command({ operation: 'popup' })).text === null, 'popup invented a selection');
          // Select the actual rendered sentence using a mouse drag across its bounding range.
          const sentence = page.locator('[data-block="letter-one-slow"] .source-text');
          const range = await sentence.evaluate(element => {
            const selection = new Range(); selection.selectNodeContents(element);
            const rects = Array.from(selection.getClientRects());
            return { first: rects[0].toJSON(), last: rects.at(-1).toJSON() };
          });
          await page.mouse.move(range.first.x + 1, range.first.y + range.first.height / 2);
          await page.mouse.down();
          await page.mouse.move(range.last.right - 1, range.last.y + range.last.height / 2, { steps: 12 });
          await page.mouse.up();
          // Make the exact full sentence selection deterministic across local CJK font metrics.
          await sentence.evaluate(element => { const range = new Range(); range.selectNodeContents(element); const selection = getSelection(); selection.removeAllRanges(); selection.addRange(range); document.dispatchEvent(new Event('selectionchange')); });
          const response = await command({ operation: 'popup', style: { allowDragging: true, closeOnOutsideClick: true } });
          assert(response.text?.includes('Đi chậm'), 'popup did not use actual selected sentence');
          await page.waitForTimeout(300);
          await page.screenshot({ path: `.tmp/sample-qa/${app}-popup-${deviceScaleFactor}.png` });
          const before = await page.locator('.reading-popup').boundingBox();
          const header = await page.locator('.reading-popup header').boundingBox();
          await page.mouse.move(header.x + 25, header.y + 10); await page.mouse.down();
          await page.mouse.move(header.x + 85, header.y - 35, { steps: 8 }); await page.mouse.up();
          assert((await page.locator('.reading-popup').boundingBox()).x > before.x + 20, 'popup drag failed');
          await page.getByLabel('Đóng bản dịch').click();
          assert(await page.locator('.reading-popup').count() === 0, 'popup close failed');
          await page.locator('.mail-list > button').nth(1).click();
          assert(await page.locator('[data-block="letter-two-meet"]').count() === 1, 'second letter failed');
        } else {
          if (app === 'article') {
            const bounds = await page.locator('[data-block="work-title"]').evaluate(element => element.getBoundingClientRect().toJSON());
            const response = await page.evaluate(bounds => window.sampleCommand({ request: { operation: 'start', kind: 'region' }, viewportBounds: bounds }), bounds);
            assert(response.ok, 'region failed');
            await page.waitForTimeout(600);
            assert(await page.locator('.reading-translation').count() === 1, 'region translated unselected text');
            await command({ operation: 'clear' });
          }
          assert((await command({ operation: 'start', kind: 'window', style: { dark: true, opacity: .96 } })).ok, `${app}: start failed`);
          await page.waitForTimeout(app === 'game' ? 3700 : 1400);
          assert(await page.locator('.reading-translation').count() > 0, `${app}: missing translations`);
          if (app === 'game') {
            const missing = await page.locator('.source-block').evaluateAll(elements => elements.filter(element =>
              /[\u3040-\u30ff\u3400-\u9fff]/.test(element.querySelector('.source-text')?.textContent || '')
              && !element.querySelector(':scope > .reading-translation')).map(element => element.dataset.block));
            assert(missing.length === 0, `game: untranslated Japanese blocks ${missing.join(', ')}`);
            await page.locator('.quest-track').click();
            await page.waitForTimeout(300);
            assert((await page.locator('[data-block="game-tracking"] .reading-translation').textContent()) === '✓ Tracking', 'game: tracking action was not translated');
          }
          await page.screenshot({ path: `.tmp/sample-qa/${app}-translated-${deviceScaleFactor}.png` });
          const clipped = await page.locator('.reading-translation').evaluateAll(elements => elements.filter(element => element.scrollHeight > element.clientHeight + 2 || element.scrollWidth > element.clientWidth + 2).length);
          assert(clipped === 0, `${app}: clipped translations`);
          if (app === 'comic') {
            const misaligned = await page.locator('.speech-bubble .reading-translation').evaluateAll(elements => elements.filter(element => {
              const text = element.getBoundingClientRect(), bubble = element.parentElement.getBoundingClientRect();
              const style = getComputedStyle(element);
              return Math.abs((text.left + text.right - bubble.left - bubble.right) / 2) > 1
                || Math.abs((text.top + text.bottom - bubble.top - bubble.bottom) / 2) > 1
                || style.backgroundColor !== 'rgba(0, 0, 0, 0)' || style.borderTopWidth !== '0px';
            }).length);
            assert(misaligned === 0, 'comic: translated text does not align with the original speech bubble');
          }
          if (app === 'game') await page.locator('.quest-list button').nth(1).click();
          if (app === 'comic') await page.locator('.comic-navigation button').last().click();
          if (app === 'article') {
            await page.locator('.article-scroll').evaluate(element => element.scrollTop = element.scrollHeight);
            await command({ operation: 'refresh' }); await page.waitForTimeout(1000);
            assert(await page.locator('[data-block="work-close"] .reading-translation').count() === 1, 'scroll did not translate newly visible content');
            await page.locator('.reader-sidebar > button').nth(1).click();
          }
          await page.waitForTimeout(app === 'game' ? 3700 : 1400);
          assert(await page.locator('.reading-translation').count() > 0, `${app}: no results on second page`);
          if (app === 'game') {
            const missing = await page.locator('.source-block').evaluateAll(elements => elements.filter(element =>
              /[\u3040-\u30ff\u3400-\u9fff]/.test(element.querySelector('.source-text')?.textContent || '')
              && !element.querySelector(':scope > .reading-translation')).map(element => element.dataset.block));
            assert(missing.length === 0, `game: untranslated Japanese blocks on second quest ${missing.join(', ')}`);
          }
          const secondPage = {
            article: ['notice-title', 'Nghệ thuật quan sát'],
            game: ['quest-two-title', 'The Lighthouse Promise'],
            comic: ['comic-two-rain', 'Cuối cùng mưa cũng tạnh rồi.']
          }[app];
          assert((await page.locator(`[data-block="${secondPage[0]}"] .reading-translation`).textContent()) === secondPage[1], `${app}: stale translation after changing page`);
          await command({ operation: 'clear' }); await page.waitForTimeout(700);
          assert(await page.locator('.reading-translation').count() === 0, 'stale results after clear');
          await command({ operation: 'start', kind: 'window' });
          await command({ operation: 'stop' });
          await page.waitForTimeout(1000);
          assert(await page.locator('.reading-translation').count() === 0, 'results remained after stop');
        }
        await page.setViewportSize({ width: 800, height: 600 });
        await page.screenshot({ path: `.tmp/sample-qa/${app}-800-${deviceScaleFactor}.png` });
        await page.setViewportSize({ width: 1240, height: 820 });
      }
      assert(external.length === 0, `external requests: ${external.join(', ')}`);
      await context.close();
    }
    const context = await browser.newContext({ viewport: { width: 2560, height: 1440 }, deviceScaleFactor: 1 });
    try {
      const page = await context.newPage();
      page.on('pageerror', error => errors.push(error.message));
      await page.goto(`${baseUrl}/sample.html?app=article`);
      await page.evaluate(() => document.fonts.ready);
      const scrollable = await page.locator('.article-scroll').evaluate(element => element.scrollHeight > element.clientHeight);
      assert(scrollable, '2K article does not scroll');
      await page.screenshot({ path: '.tmp/sample-qa/article-2k-source.png' });
      const introBounds = await page.locator('[data-block="work-intro"]').evaluate(element => element.getBoundingClientRect().toJSON());
      const command = request => page.evaluate(request => window.sampleCommand({ request }), request);
      assert((await page.evaluate(bounds => window.sampleCommand({ request: { operation: 'start', kind: 'region' }, viewportBounds: bounds }), introBounds)).ok, '2K region failed');
      await page.waitForTimeout(300);
      assert(await page.locator('[data-block="work-intro"] .reading-translation').count() === 1, '2K selected paragraph was not translated');
      await page.locator('.article-scroll').evaluate(element => { element.scrollTop += 600; });
      await page.waitForTimeout(1000);
      assert(await page.locator('[data-block="work-intro"] .reading-translation').count() === 0, '2K region result remained outside selection');
      assert(await page.locator('.reading-translation').count() > 0, '2K region did not translate newly entering content');
      await page.screenshot({ path: '.tmp/sample-qa/article-2k-region-scrolled.png' });
      await command({ operation: 'stop' });
      assert(await page.locator('.reading-translation').count() === 0, '2K stop left translations visible');
      assert((await command({ operation: 'start', kind: 'window' })).ok, '2K window mode failed');
      await page.locator('.reader-sidebar > button').nth(1).click();
      await page.waitForTimeout(900);
      assert(await page.locator('[data-block="notice-title"] .reading-translation').count() === 1, '2K window mode did not translate the next article');
      assert((await page.locator('[data-block="notice-title"] .reading-translation').textContent()) === 'Nghệ thuật quan sát', '2K article kept the previous translation');
      await page.screenshot({ path: '.tmp/sample-qa/article-2k-window-next.png' });
    } finally { await context.close(); }
    const remaining = await browser.newContext({ viewport: { width: 2560, height: 1440 }, deviceScaleFactor: 1 });
    try {
      const page = await remaining.newPage();
      page.on('pageerror', error => errors.push(error.message));
      const command = request => page.evaluate(request => window.sampleCommand({ request }), request);
      for (const app of ['game', 'comic']) {
        await page.goto(`${baseUrl}/sample.html?app=${app}`);
        await page.evaluate(() => document.fonts.ready);
        assert((await command({ operation: 'start', kind: 'window' })).ok, `2K ${app}: start failed`);
        await page.waitForTimeout(app === 'game' ? 3700 : 1100);
        const translation = page.locator('.reading-translation');
        assert(await translation.count() > 0, `2K ${app}: first page has no translation`);
        if (app === 'game') {
          const missing = await page.locator('.source-block').evaluateAll(elements => elements.filter(element =>
            /[\u3040-\u30ff\u3400-\u9fff]/.test(element.querySelector('.source-text')?.textContent || '')
            && !element.querySelector(':scope > .reading-translation')).map(element => element.dataset.block));
          assert(missing.length === 0, `2K game: untranslated Japanese blocks ${missing.join(', ')}`);
        }
        await page.screenshot({ path: `.tmp/sample-qa/${app}-2k-page-1.png` });
        if (app === 'game') {
          await page.locator('.quest-list button').nth(1).click();
          assert(await page.locator('[data-block="quest-one-title"]').count() === 0, '2K game: old quest remained');
        } else {
          await page.locator('.comic-navigation button').last().click();
          assert(await page.locator('[data-block="comic-one-star"]').count() === 0, '2K comic: old page remained');
        }
        await page.waitForTimeout(app === 'game' ? 3700 : 1100);
        assert(await translation.count() > 0, `2K ${app}: second page has no translation`);
        const secondPage = app === 'game'
          ? ['quest-two-title', 'The Lighthouse Promise']
          : ['comic-two-rain', 'Cuối cùng mưa cũng tạnh rồi.'];
        assert((await page.locator(`[data-block="${secondPage[0]}"] .reading-translation`).textContent()) === secondPage[1], `2K ${app}: stale translation after changing page`);
        if (app === 'comic') {
          const overflow = await page.locator('.speech-bubble .reading-translation').evaluateAll(elements => elements.some(element => {
            const text = element.getBoundingClientRect(), bubble = element.parentElement.getBoundingClientRect();
            const style = getComputedStyle(element);
            return text.left < bubble.left - 1 || text.top < bubble.top - 1 || text.right > bubble.right + 1 || text.bottom > bubble.bottom + 1 || element.scrollHeight > element.clientHeight + 2
              || Math.abs((text.left + text.right - bubble.left - bubble.right) / 2) > 1
              || Math.abs((text.top + text.bottom - bubble.top - bubble.bottom) / 2) > 1
              || style.backgroundColor !== 'rgba(0, 0, 0, 0)' || style.borderTopWidth !== '0px';
          }));
          assert(!overflow, '2K comic: translation escaped or overflowed a speech bubble');
        }
        await page.screenshot({ path: `.tmp/sample-qa/${app}-2k-page-2.png` });
        await command({ operation: 'stop' });
        await page.waitForTimeout(900);
        assert(await translation.count() === 0, `2K ${app}: stop left translations visible`);
        assert((await command({ operation: 'start', kind: 'window' })).ok, `2K ${app}: restart failed`);
        await page.waitForTimeout(900);
        assert(await translation.count() > 0, `2K ${app}: restart has no translation`);
        await command({ operation: 'clear' });
      }
      await page.goto(`${baseUrl}/sample.html?app=letters`);
      await page.evaluate(() => document.fonts.ready);
      const select = async id => page.locator(`[data-block="${id}"] .source-text`).evaluate(element => {
        const range = new Range(); range.selectNodeContents(element);
        const selection = getSelection(); selection.removeAllRanges(); selection.addRange(range);
        document.dispatchEvent(new Event('selectionchange'));
      });
      await select('letter-one-slow');
      assert((await command({ operation: 'popup' })).text?.includes('Đi chậm'), '2K letters: first selection failed');
      await page.waitForTimeout(250);
      assert(await page.locator('.reading-popup').isVisible(), '2K letters: first popup did not stay open');
      await page.screenshot({ path: '.tmp/sample-qa/letters-2k-popup-1.png' });
      await command({ operation: 'stop' });
      assert(await page.locator('.reading-popup').count() === 0, '2K letters: stop left popup visible');
      await select('letter-one-start');
      assert((await command({ operation: 'popup' })).text?.includes('khởi đầu'), '2K letters: second selection failed');
      await page.waitForTimeout(250);
      assert(await page.locator('.reading-popup').isVisible(), '2K letters: second popup did not stay open');
      await page.locator('.mail-list > button').nth(1).click();
      assert(await page.locator('.reading-popup').count() === 0, '2K letters: popup remained after changing letter');
      await select('letter-two-meet');
      assert((await command({ operation: 'popup' })).text?.includes('bờ sông'), '2K letters: second letter failed');
      await page.waitForTimeout(250);
      assert(await page.locator('.reading-popup').isVisible(), '2K letters: second letter popup did not stay open');
      await page.screenshot({ path: '.tmp/sample-qa/letters-2k-popup-2.png' });
      await command({ operation: 'clear' });
      assert(await page.locator('.reading-popup').count() === 0, '2K letters: clear left popup visible');
    } finally { await remaining.close(); }
    assert(errors.length === 0, errors.join('\n'));
    console.log('Sample QA passed: four independent apps, 100%/150%, 2K article/game/letters/comic flows, selection popup, drag/close, stop/clear/restart, offline assets.');
  } finally { await browser.close(); }
}
