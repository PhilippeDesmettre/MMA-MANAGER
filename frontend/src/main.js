import { createApp } from 'vue'
import { createVuetify } from 'vuetify'
import App from './App.vue'
import router from './router'
import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import './style.css'

const vuetify = createVuetify({ icons: { defaultSet: 'mdi' } })

createApp(App).use(vuetify).use(router).mount('#app')
