<script setup lang="ts">
import { onBeforeUnmount, onMounted, watch } from 'vue'
import { Chart, BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend } from 'chart.js'

Chart.register(BarController, BarElement, CategoryScale, LinearScale, Tooltip, Legend)

const props = defineProps<{
  labels: string[]
  values: number[]
  title?: string
}>()

let chart: Chart | null = null

function render() {
  const canvas = document.getElementById('report-chart') as HTMLCanvasElement | null
  if (!canvas) return

  chart?.destroy()
  chart = new Chart(canvas, {
    type: 'bar',
    data: {
      labels: props.labels,
      datasets: [
        {
          label: props.title || 'Views',
          data: props.values,
          borderRadius: 10,
          backgroundColor: '#0ea5e9',
        },
      ],
    },
    options: {
      responsive: true,
      plugins: { legend: { display: false } },
      scales: {
        y: { beginAtZero: true, ticks: { precision: 0 } },
      },
    },
  })
}

onMounted(render)
watch(() => [props.labels, props.values], render, { deep: true })
onBeforeUnmount(() => chart?.destroy())
</script>

<template>
  <div class="panel">
    <canvas id="report-chart" style="min-height: 320px;" />
  </div>
</template>
