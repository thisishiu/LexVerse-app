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
          await page.waitForTimeout(1400);
          assert(await page.locator('.reading-translation').count() > 0, `${app}: missing translations`);
          await page.screenshot({ path: `.tmp/sample-qa/${app}-translated-${deviceScaleFactor}.png` });
          const clipped = await page.locator('.reading-translation').evaluateAll(elements => elements.filter(element => element.scrollHeight > element.clientHeight + 2 || element.scrollWidth > element.clientWidth + 2).length);
          assert(clipped === 0, `${app}: clipped translations`);
          if (app === 'game') await page.locator('.quest-list button').nth(1).click();
          if (app === 'comic') await page.locator('.comic-navigation button').last().click();
          if (app === 'article') {
            await page.locator('.article-scroll').evaluate(element => element.scrollTop = element.scrollHeight);
            await command({ operation: 'refresh' }); await page.waitForTimeout(1000);
            assert(await page.locator('[data-block="work-break"] .reading-translation').count() === 1, 'scroll did not translate newly visible content');
            await page.locator('.reader-sidebar > button').nth(1).click();
          }
          await page.waitForTimeout(1400);
          assert(await page.locator('.reading-translation').count() > 0, `${app}: no results on second page`);
          await command({ operation: 'clear' }); await page.waitForTimeout(700);
          assert(await page.locator('.reading-translation').count() === 0, 'stale results after clear');
          await command({ operation: 'start', kind: 'window' });
          await command({ operation: 'stop' });
          const count = await page.locator('.reading-translation').count();
          await page.waitForTimeout(1000);
          assert(await page.locator('.reading-translation').count() === count, 'stale pending results after stop');
        }
        await page.setViewportSize({ width: 800, height: 600 });
        await page.screenshot({ path: `.tmp/sample-qa/${app}-800-${deviceScaleFactor}.png` });
        await page.setViewportSize({ width: 1240, height: 820 });
      }
      assert(external.length === 0, `external requests: ${external.join(', ')}`);
      await context.close();
    }
    assert(errors.length === 0, errors.join('\n'));
    console.log('Sample QA passed: four independent apps, 100%/150%, region bounds, selection popup, drag/close, scroll, second pages, clear/stop, offline assets.');
  } finally { await browser.close(); }
}
