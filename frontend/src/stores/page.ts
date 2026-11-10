import { reactive } from 'vue'

const Page: {
    loaded: boolean
    user: object | null
    sidebar: boolean
    menu: 'desktop' | 'mobile'
    successLoading: boolean
} = reactive({
    loaded: false,
    user: null,
    sidebar: true,
    menu: 'desktop',
    successLoading: false,
})

export default Page