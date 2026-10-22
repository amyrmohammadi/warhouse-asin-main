import 'datatables.net-bs4/css/dataTables.bootstrap4.css'
import 'datatables.net-responsive-bs4/css/responsive.bootstrap4.css'
import 'datatables.net-responsive-bs4'
import './assets/main.css'
import './assets/style.css'

import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { applyTheme, getStoredTheme } from './services/theme'

applyTheme(getStoredTheme())

const app = createApp(App)

app.use(router)

app.mount('#app')
