import type { ReactNode } from 'react'
import { ConfigProvider } from 'antd'
import './order-controls.css'

// Scope the draft's visual language to customer order routes, including portals.
export function OrderUiProvider({ children }: { children: ReactNode }) {
  return <ConfigProvider
    theme={{ token: {
      colorPrimary: '#1e4f3c', colorPrimaryHover: '#365f45', colorPrimaryActive: '#163f2f',
      colorText: '#254434', colorTextSecondary: '#627365', colorTextPlaceholder: '#73816e',
      colorBorder: '#c5d3bc', colorBgContainer: '#fffffc', colorBgElevated: '#fffffc',
      colorError: '#a34b3c', colorWarning: '#a17927', colorInfo: '#4b7353', colorSuccess: '#4c7a56',
      controlOutline: '#4c7a561c', controlHeight: 42, borderRadius: 8, borderRadiusLG: 14,
      fontFamily: "'Segoe UI', Arial, sans-serif", fontSize: 14,
    }, components: {
      Button: { fontWeight: 600, defaultBg: '#fffffc', defaultColor: '#36513f', defaultBorderColor: '#c5d3bc', defaultHoverBg: '#eef4e8', defaultHoverColor: '#1e4f3c', defaultHoverBorderColor: '#9db493', primaryShadow: '0 2px 0 #143d2e', dangerShadow: 'none' },
      Select: { optionSelectedBg: '#e4eddb', optionSelectedColor: '#1e4f3c', optionActiveBg: '#f0f5e9', optionHeight: 40, optionPadding: '10px 12px' },
      Input: { activeBorderColor: '#4c7a56', hoverBorderColor: '#94ad94', activeShadow: '0 0 0 3px #4c7a5617' },
      Modal: { headerBg: '#fffffc', contentBg: '#fffffc', titleColor: '#203b30', titleFontSize: 21 },
      Table: { headerBg: '#e4eddb', headerColor: '#36513f', rowHoverBg: '#f1f5ea', borderColor: '#e1e8dc', cellPaddingBlock: 18 },
      Pagination: { itemActiveBg: '#e4eddb', itemSize: 36 },
      DatePicker: { activeBorderColor: '#4c7a56', hoverBorderColor: '#94ad94', activeShadow: '0 0 0 3px #4c7a5617', cellActiveWithRangeBg: '#e4eddb' },
    } }}
    modal={{ centered: true, classNames: { container: 'order-dialog', header: 'order-dialog-header', body: 'order-dialog-body', footer: 'order-dialog-footer' } }}
    select={{ classNames: { popup: { root: 'order-select-popup' } } }}
    datePicker={{ classNames: { popup: { root: 'order-calendar-popup' } } }}
    form={{ scrollToFirstError: true }}
  >{children}</ConfigProvider>
}
