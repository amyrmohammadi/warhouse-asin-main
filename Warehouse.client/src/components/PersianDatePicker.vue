
<script setup lang="ts">
import { ref } from 'vue'
import DatePicker from 'vue3-persian-datetime-picker'

// مقدار میلادی برای ارسال به سرور
const dateTime = ref<string>('')

function save() {
  console.log('Gregorian:', dateTime.value)
}
</script>

<template>
  <div class="date-time-field" dir="rtl">
    <label>تاریخ و ساعت</label>

    <DatePicker
      v-model="dateTime"
      type="datetime"
      format="YYYY-MM-DDTHH:mm:ss"
      display-format="jYYYY/jMM/jDD HH:mm"
      locale="fa"
      placeholder="انتخاب تاریخ و ساعت"
      :editable="true"
      :clearable="true"
      :auto-submit="false"
      color="#0f766e"
    />

    <button type="button" @click="save">
      ذخیره
    </button>

    <p>مقدار ارسالی: {{ dateTime }}</p>
  </div>
</template>

<style scoped>
.date-time-field {
  max-width: 350px;
}

.date-time-field label {
  display: block;
  margin-bottom: 8px;
}

.date-time-field :deep(input) {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: var(--surface, #fff);
  color: var(--text-primary, #1e293b);
}
</style>
