/**
 * ECharts 按需注册（控制打包体积）：折线/柱状/饼图 + 常用组件。
 * 用法：import { initChart } from '@/utils/chart'
 */
import * as echarts from 'echarts/core'
import { LineChart, PieChart, BarChart } from 'echarts/charts'
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DataZoomComponent
} from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsOption } from 'echarts'

echarts.use([
  LineChart,
  PieChart,
  BarChart,
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
  DataZoomComponent,
  CanvasRenderer
])

export function initChart(el: HTMLElement): echarts.ECharts {
  return echarts.init(el)
}

export type { EChartsOption }
export { echarts }
