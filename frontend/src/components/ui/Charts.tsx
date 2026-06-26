import { cn } from "@/lib/utils";
import { theme } from "@/theme/tokens";

interface SparklineProps {
  data: number[];
  positive?: boolean;
  width?: number;
  height?: number;
  className?: string;
}

export function Sparkline({
  data,
  positive = true,
  width = 72,
  height = 28,
  className,
}: SparklineProps) {
  if (data.length < 2) return null;

  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;

  const points = data
    .map((v, i) => {
      const x = (i / (data.length - 1)) * width;
      const y = height - ((v - min) / range) * (height - 4) - 2;
      return `${x},${y}`;
    })
    .join(" ");

  const color = positive ? theme.green : theme.red;

  return (
    <svg width={width} height={height} className={cn("shrink-0", className)} aria-hidden>
      <polyline
        fill="none"
        stroke={color}
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        points={points}
      />
    </svg>
  );
}

interface LineChartProps {
  data: number[];
  positive?: boolean;
  height?: number;
  className?: string;
}

export function LineChart({ data, positive = true, height = 140, className }: LineChartProps) {
  const width = 320;
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;

  const coords = data.map((v, i) => ({
    x: (i / (data.length - 1)) * width,
    y: height - 20 - ((v - min) / range) * (height - 36),
  }));

  const line = coords.map((p) => `${p.x},${p.y}`).join(" ");
  const area = `${coords[0].x},${height} ${line} ${coords[coords.length - 1].x},${height}`;
  const color = positive ? theme.green : theme.red;

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className={cn("w-full", className)} aria-hidden>
      <defs>
        <linearGradient id="chartFill" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.18" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      {[0.25, 0.5, 0.75].map((p) => (
        <line
          key={p}
          x1="0"
          y1={height * p}
          x2={width}
          y2={height * p}
          stroke={theme.border}
          strokeWidth="1"
        />
      ))}
      <polygon points={area} fill="url(#chartFill)" />
      <polyline
        fill="none"
        stroke={color}
        strokeWidth="2.5"
        strokeLinecap="round"
        strokeLinejoin="round"
        points={line}
      />
    </svg>
  );
}

interface VolumeBarChartProps {
  data: number[];
  className?: string;
}

export function VolumeBarChart({ data, className }: VolumeBarChartProps) {
  const width = 320;
  const height = 80;
  const max = Math.max(...data, 1);
  const barW = width / data.length - 4;

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className={cn("w-full", className)} aria-hidden>
      {data.map((v, i) => {
        const h = (v / max) * (height - 10);
        const x = i * (barW + 4) + 2;
        return (
          <rect
            key={i}
            x={x}
            y={height - h}
            width={barW}
            height={h}
            rx="3"
            fill={theme.blue}
            opacity={0.55 + (i / data.length) * 0.45}
          />
        );
      })}
    </svg>
  );
}
