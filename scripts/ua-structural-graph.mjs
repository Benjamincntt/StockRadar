#!/usr/bin/env node
/**
 * Structural-only /understand for StockRadar — no LLM batches.
 * Produces batch-*.json from extract-structure.mjs, then merge-batch-graphs.py.
 */
import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, dirname, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const projectRoot = process.argv[2] || 'D:/Source/StockRadar';
const skillDir = process.argv[3] || join(process.env.USERPROFILE || '', '.understand-anything/repo/understand-anything-plugin/skills/understand');
const extractScript = join(skillDir, 'extract-structure.mjs');
const mergeScript = join(skillDir, 'merge-batch-graphs.py');
const uaDir = join(projectRoot, '.understand-anything');
const inter = join(uaDir, 'intermediate');
const tmp = join(uaDir, 'tmp');

mkdirSync(tmp, { recursive: true });

const batches = JSON.parse(readFileSync(join(inter, 'batches.json'), 'utf8'));
const scan = JSON.parse(readFileSync(join(inter, 'scan-result.json'), 'utf8'));

function nodeTypeFor(file) {
  const cat = file.fileCategory;
  const p = file.path.replace(/\\/g, '/');
  if (cat === 'docs' || p.endsWith('.md')) return 'document';
  if (cat === 'config' || p.endsWith('.json') || p.endsWith('.yaml') || p.endsWith('.yml')) return 'config';
  if (p.includes('Dockerfile') || p.endsWith('.tf')) return 'service';
  if (p.includes('.github/workflows')) return 'pipeline';
  return 'file';
}

function summaryVi(file, fnCount, clsCount) {
  const name = basename(file.path);
  if (file.fileCategory === 'docs') return `Tài liệu: ${name}`;
  if (file.language === 'csharp') return `Mã C# (${fnCount} hàm, ${clsCount} lớp): ${file.path}`;
  if (file.language === 'dart') return `Mã Dart/Flutter: ${file.path}`;
  if (file.language === 'typescript') return `Mã TypeScript React: ${file.path}`;
  return `File ${file.language || 'unknown'}: ${file.path}`;
}

function tagsFor(file) {
  const p = file.path.replace(/\\/g, '/');
  const t = [];
  if (p.startsWith('backend/')) t.push('backend', 'dotnet');
  if (p.startsWith('mobile/')) t.push('mobile', 'flutter');
  if (p.startsWith('frontend/')) t.push('frontend', 'react');
  if (p.includes('Controller')) t.push('api-handler');
  if (p.includes('Test')) t.push('test');
  if (p.endsWith('README.md')) t.push('documentation', 'entry-point');
  if (t.length === 0) t.push('utility');
  return t.slice(0, 5);
}

console.log(`[Phase 2/7] Structural extract — ${batches.totalBatches} batches...`);

for (const batch of batches.batches) {
  const idx = batch.batchIndex;
  const inputPath = join(tmp, `ua-input-${idx}.json`);
  const extractOut = join(tmp, `ua-extract-${idx}.json`);
  const batchOut = join(inter, `batch-${idx}.json`);

  writeFileSync(inputPath, JSON.stringify({
    projectRoot,
    batchFiles: batch.files,
    batchImportData: batch.batchImportData || {},
  }));

  const r = spawnSync(process.execPath, [extractScript, inputPath, extractOut], { encoding: 'utf8' });
  if (r.status !== 0) {
    console.error(`batch ${idx} extract failed:`, r.stderr);
    continue;
  }

  const extracted = JSON.parse(readFileSync(extractOut, 'utf8'));
  const nodes = [];
  const edges = [];

  for (const file of batch.files) {
    const type = nodeTypeFor(file);
    const id = `${type}:${file.path.replace(/\\/g, '/')}`;
    const res = extracted.results?.find((x) => x.path === file.path);
    const fnCount = res?.functions?.length ?? 0;
    const clsCount = res?.classes?.length ?? 0;

    nodes.push({
      id,
      type,
      name: basename(file.path),
      filePath: file.path.replace(/\\/g, '/'),
      summary: summaryVi(file, fnCount, clsCount),
      tags: tagsFor(file),
      complexity: file.sizeLines > 300 ? 'complex' : file.sizeLines > 120 ? 'moderate' : 'simple',
    });

    const imports = batch.batchImportData?.[file.path] || [];
    for (const imp of imports) {
      const targetType = imp.endsWith('.md') ? 'document' : 'file';
      edges.push({
        source: id,
        target: `${targetType}:${imp}`,
        type: 'imports',
        direction: 'forward',
        weight: 0.7,
      });
    }

    for (const fn of res?.functions || []) {
      if ((fn.endLine - fn.startLine) < 10 && !res.exports?.some((e) => e.name === fn.name)) continue;
      const fnId = `function:${file.path.replace(/\\/g, '/')}:${fn.name}`;
      nodes.push({
        id: fnId,
        type: 'function',
        name: fn.name,
        filePath: file.path.replace(/\\/g, '/'),
        summary: `Hàm ${fn.name} (${file.path})`,
        tags: tagsFor(file),
        complexity: 'simple',
      });
      edges.push({ source: id, target: fnId, type: 'contains', direction: 'forward', weight: 1.0 });
    }

    for (const cls of res?.classes || []) {
      const clsId = `class:${file.path.replace(/\\/g, '/')}:${cls.name}`;
      nodes.push({
        id: clsId,
        type: 'class',
        name: cls.name,
        filePath: file.path.replace(/\\/g, '/'),
        summary: `Lớp ${cls.name} (${file.path})`,
        tags: tagsFor(file),
        complexity: 'moderate',
      });
      edges.push({ source: id, target: clsId, type: 'contains', direction: 'forward', weight: 1.0 });
    }
  }

  writeFileSync(batchOut, JSON.stringify({ nodes, edges }, null, 2));
  console.log(`  batch ${idx}/${batches.totalBatches - 1} -> ${nodes.length} nodes`);
}

console.log('[merge] merge-batch-graphs.py...');
const m = spawnSync('py', [mergeScript, projectRoot], { encoding: 'utf8' });
if (m.stderr) process.stderr.write(m.stderr);
if (m.status !== 0) {
  console.error('merge failed', m.stdout, m.stderr);
  process.exit(1);
}

const assembled = JSON.parse(readFileSync(join(inter, 'assembled-graph.json'), 'utf8'));
const fileNodes = assembled.nodes.filter((n) =>
  ['file', 'config', 'document', 'service', 'pipeline'].includes(n.type),
);

function layerNodes(prefix) {
  return fileNodes
    .filter((n) => n.filePath?.startsWith(prefix))
    .map((n) => n.id);
}

const layers = [
  {
    id: 'layer:backend-api',
    name: 'Backend .NET API',
    description: 'Controllers, services, domain engines, infrastructure jobs',
    nodeIds: layerNodes('backend/'),
  },
  {
    id: 'layer:mobile-flutter',
    name: 'Mobile Flutter',
    description: 'Screens, widgets, navigation, API client',
    nodeIds: layerNodes('mobile/'),
  },
  {
    id: 'layer:frontend-web',
    name: 'Web React',
    description: 'Pages và components Vite/React',
    nodeIds: layerNodes('frontend/'),
  },
  {
    id: 'layer:scripts-ops',
    name: 'Scripts & Deploy',
    description: 'Deploy, build, pipeline scripts',
    nodeIds: layerNodes('scripts/'),
  },
].filter((l) => l.nodeIds.length > 0);

const tour = [
  {
    order: 1,
    title: 'Tổng quan StockRadar',
    description: 'JUICE — monorepo API .NET + Flutter + React. Đọc README và CLAUDE.md.',
    nodeIds: ['document:README.md', 'document:CLAUDE.md'].filter((id) =>
      assembled.nodes.some((n) => n.id === id),
    ),
  },
  {
    order: 2,
    title: 'Pipeline dữ liệu',
    description: 'Job sync OHLCV → phân tích SmartMoney → criterion scoring.',
    nodeIds: assembled.nodes
      .filter((n) => /DailyAnalysis|MarketJobs|Criterion/i.test(n.filePath || ''))
      .slice(0, 8)
      .map((n) => n.id),
  },
  {
    order: 3,
    title: 'Mobile app',
    description: 'Entry: app_router, api_client, các màn hình chính.',
    nodeIds: assembled.nodes
      .filter((n) => n.filePath?.startsWith('mobile/lib/'))
      .slice(0, 10)
      .map((n) => n.id),
  },
];

const commit = spawnSync('git', ['-C', projectRoot, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).stdout.trim();

const graph = {
  version: '1.0.0',
  project: {
    name: scan.name || 'StockRadar',
    languages: Object.keys(scan.stats?.byLanguage || {}),
    frameworks: scan.frameworks || ['aspnetcore', 'flutter', 'react'],
    description: scan.rawDescription || 'Smart Money Monitor — JUICE',
    analyzedAt: new Date().toISOString(),
    gitCommitHash: commit,
    structuralOnly: true,
  },
  nodes: assembled.nodes,
  edges: assembled.edges,
  layers,
  tour: tour.filter((t) => t.nodeIds.length > 0),
};

writeFileSync(join(uaDir, 'knowledge-graph.json'), JSON.stringify(graph, null, 2));
writeFileSync(join(uaDir, 'meta.json'), JSON.stringify({ gitCommitHash: commit, structuralOnly: true }, null, 2));

console.log(`Done: ${graph.nodes.length} nodes, ${graph.edges.length} edges`);
console.log(`Output: ${join(uaDir, 'knowledge-graph.json')}`);
