<template>
  <div class="container">
    <div class="section-head">
      <div>
        <h1>Reports</h1>
        <p class="muted">Аналитика по просмотрам квартир и районов.</p>
      </div>
    </div>

    <div class="property-grid">
      <RouterLink to="/reports/most-viewed-apartments" class="panel">
        <h3>Most viewed apartments</h3>
        <p class="muted">Топ квартир по просмотрам с карточками и графиком.</p>
      </RouterLink>

      <RouterLink to="/reports/most-viewed-districts" class="panel">
        <h3>Most viewed districts</h3>
        <p class="muted">Просмотры по районам за выбранный период.</p>
      </RouterLink>
    </div>
  </div>
</template>
