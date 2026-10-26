import { computed, ref } from 'vue'

const stack = ref<number[]>([])
let sequence = 0

/** پشتهٔ سراسری مودال‌ها: فقط بالاترین مودال به Esc پاسخ می‌دهد و قفل اسکرول یک‌بار اعمال می‌شود. */
export function useModalStack() {
  const id = ++sequence
  const isTop = computed(() => stack.value.at(-1) === id)

  function push(): void {
    if (stack.value.includes(id)) return
    stack.value = [...stack.value, id]
    document.body.classList.add('modal-open')
  }
  function remove(): void {
    stack.value = stack.value.filter(item => item !== id)
    if (!stack.value.length) document.body.classList.remove('modal-open')
  }
  return { push, remove, isTop }
}
