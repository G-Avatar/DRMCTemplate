import { reactive } from 'vue'
import type { IAccount } from '@/interfaces/global/IAccount'

const Page: {
    loaded: boolean
    user: IAccount | null
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