export interface SpriteFrame {
  start: number;
  end: number;
  url: string;
  x: number;
  y: number;
  w: number;
  h: number;
}

export function parseVtt(text: string, baseUrl = ''): SpriteFrame[] {
  return text
    .split(/\n\n+/)
    .filter(block => block.includes('-->'))
    .flatMap(block => {
      const lines = block.trim().split('\n');
      const timeLine = lines.find(l => l.includes('-->'));
      const urlLine = lines[lines.length - 1];
      if (!timeLine) return [];

      const [startStr, endStr] = timeLine.split('-->').map(s => s.trim());
      const match = urlLine.match(/#xywh=(\d+),(\d+),(\d+),(\d+)/);
      if (!match) return [];

      const rawUrl = urlLine.split('#')[0].trim();
      const resolvedUrl = baseUrl ? new URL(rawUrl, baseUrl).href : rawUrl;

      return [{
        start: toSeconds(startStr),
        end: toSeconds(endStr),
        url: resolvedUrl,
        x: +match[1],
        y: +match[2],
        w: +match[3],
        h: +match[4],
      }];
    });
}

function toSeconds(t: string): number {
  const parts = t.split(':').reverse().map(parseFloat);
  return parts.reduce((acc, p, i) => acc + p * 60 ** i, 0);
}
