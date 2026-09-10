import { Area, AreaChart, CartesianGrid, XAxis, PolarAngleAxis, PolarGrid, Radar, RadarChart, YAxis, Bar, BarChart } from "recharts"

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
  type ChartConfig,
} from "@/components/ui/chart"

// --- Area Chart (Haftalık İşlem Sayısı) ---
const areaChartData = [
  { month: "Ocak", islem: 186 },
  { month: "Şubat", islem: 305 },
  { month: "Mart", islem: 237 },
  { month: "Nisan", islem: 73 },
  { month: "Mayıs", islem: 209 },
  { month: "Haziran", islem: 214 },
]

const areaChartConfig = {
  islem: {
    label: "İşlem Sayısı",
    color: "#2b7fff",
  },
} satisfies ChartConfig

export function ChartAreaGradient() {
  return (
    <Card className="h-full flex flex-col">
      <CardHeader>
        <CardTitle>Operatör İşlem Özeti</CardTitle>
        <CardDescription>Ocak - Haziran 2026</CardDescription>
      </CardHeader>
      <CardContent className="flex-1 pb-0">
        <ChartContainer config={areaChartConfig} className="max-h-[300px] w-full">
          <AreaChart
            accessibilityLayer
            data={areaChartData}
            margin={{
              left: 12,
              right: 12,
              top: 20
            }}
          >
            <CartesianGrid vertical={false} />
            <XAxis
              dataKey="month"
              tickLine={false}
              axisLine={false}
              tickMargin={8}
              tickFormatter={(value) => value.slice(0, 3)}
            />
            <ChartTooltip cursor={false} content={<ChartTooltipContent />} />
            <defs>
              <linearGradient id="fillIslem" x1="0" y1="0" x2="0" y2="1">
                <stop
                  offset="5%"
                  stopColor="var(--color-islem)"
                  stopOpacity={0.8}
                />
                <stop
                  offset="95%"
                  stopColor="var(--color-islem)"
                  stopOpacity={0.1}
                />
              </linearGradient>
            </defs>
            <Area
              dataKey="islem"
              type="natural"
              fill="url(#fillIslem)"
              fillOpacity={0.4}
              stroke="var(--color-islem)"
              stackId="a"
            />
          </AreaChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}

// --- Radar Chart (En Fazla İşlem Yapılan 5 Kabin) ---
const radarChartData = [
  { kabin: "KAB-01", islem: 186, ortalamaDk: 80 },
  { kabin: "KAB-02", islem: 305, ortalamaDk: 200 },
  { kabin: "KAB-03", islem: 237, ortalamaDk: 120 },
  { kabin: "KAB-04", islem: 73, ortalamaDk: 190 },
  { kabin: "KAB-05", islem: 209, ortalamaDk: 130 },
]

const radarChartConfig = {
  islem: {
    label: "İşlem Sayısı",
    color: "#2b7fff",
  },
  ortalamaDk: {
    label: "Ortalama (Dk)",
    color: "#8ec5ff",
  },
} satisfies ChartConfig

export function ChartRadarMultiple() {
  return (
    <Card className="h-full flex flex-col">
      <CardHeader className="items-center pb-4">
        <CardTitle>En Aktif 5 Kabin</CardTitle>
        <CardDescription>
          Son 1 aydaki işlemler ve ortalama süreleri
        </CardDescription>
      </CardHeader>
      <CardContent className="flex-1 pb-0">
        <ChartContainer
          config={radarChartConfig}
          className="mx-auto aspect-square max-h-[250px]"
        >
          <RadarChart data={radarChartData}>
            <ChartTooltip
              cursor={false}
              content={<ChartTooltipContent indicator="line" />}
            />
            <PolarAngleAxis dataKey="kabin" />
            <PolarGrid />
            <Radar
              dataKey="islem"
              fill="var(--color-islem)"
              fillOpacity={0.6}
            />
            <Radar dataKey="ortalamaDk" fill="var(--color-ortalamaDk)" fillOpacity={0.6} />
          </RadarChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}

// --- Mixed Bar Chart (En Fazla İşlem Yapan 5 Operatör) ---
const mixedChartData = [
  { operator: "Ahmet Y.", islem: 275 },
  { operator: "Mehmet K.", islem: 200 },
  { operator: "Ayşe S.", islem: 187 },
  { operator: "Fatma D.", islem: 173 },
  { operator: "Ali R.", islem: 90 },
]

const mixedChartConfig = {
  islem: {
    label: "İşlem Sayısı",
    color: "#2b7fff",
  },
  operator: {
    label: "Operatör",
  }
} satisfies ChartConfig

export function ChartBarMixed() {
  return (
    <Card className="h-full flex flex-col">
      <CardHeader>
        <CardTitle>Top 5 Operatör</CardTitle>
        <CardDescription>Son 6 Ay (Ocak - Haziran)</CardDescription>
      </CardHeader>
      <CardContent className="flex-1 pb-0">
        <ChartContainer config={mixedChartConfig} className="max-h-[300px] w-full">
          <BarChart
            accessibilityLayer
            data={mixedChartData}
            layout="vertical"
            margin={{
              left: 0,
            }}
          >
            <YAxis
              dataKey="operator"
              type="category"
              tickLine={false}
              tickMargin={10}
              axisLine={false}
              tickFormatter={(value) => value.split(" ")[0]}
            />
            <XAxis dataKey="islem" type="number" hide />
            <ChartTooltip
              cursor={false}
              content={<ChartTooltipContent hideLabel />}
            />
            <Bar dataKey="islem" fill="var(--color-islem)" radius={5} />
          </BarChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}

export function DashboardMetrics() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-4 gap-4 w-full mt-4">
      <div className="col-span-1 md:col-span-2">
        <ChartAreaGradient />
      </div>
      <div className="col-span-1">
        <ChartRadarMultiple />
      </div>
      <div className="col-span-1">
        <ChartBarMixed />
      </div>
    </div>
  )
}
