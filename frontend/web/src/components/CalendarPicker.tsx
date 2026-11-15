import { ConfigProvider, DatePicker, type DatePickerProps, type ThemeConfig } from 'antd'
import dayjs, { type Dayjs } from 'dayjs'
import './calendar-picker.css'

const dayFormat = 'YYYY-MM-DD'
const calendarTheme: ThemeConfig = {
  token: {
    colorPrimary: '#1e4f3c',
    colorPrimaryHover: '#365f45',
    colorText: '#203b30',
    colorTextSecondary: '#52665a',
    colorBgContainer: '#fffffc',
    colorBgElevated: '#fffffc',
    colorBorder: '#c5d3bc',
    colorLink: '#1e4f3c',
    controlItemBgActive: '#e4eddb',
    controlHeight: 42,
    borderRadius: 8,
  },
  components: {
    DatePicker: {
      activeBorderColor: '#4c7a56',
      hoverBorderColor: '#94ad94',
      activeShadow: '0 0 0 3px #4c7a5617',
      cellHoverBg: '#edf2e6',
      cellActiveWithRangeBg: '#e4eddb',
    },
  },
}

export function CalendarPicker(props: DatePickerProps<Dayjs, false>) {
  const format = props.showTime ? 'YYYY-MM-DD HH:mm' : dayFormat
  return (
    <ConfigProvider theme={calendarTheme}>
      <DatePicker
        format={format}
        placeholder={props.showTime ? 'Select date and time' : 'Select date'}
        {...props}
        classNames={{
          root: 'smartdrone-calendar-input',
          popup: { root: 'smartdrone-calendar-popup', container: 'smartdrone-calendar-panel' },
        }}
      />
    </ConfigProvider>
  )
}

type CalendarDayPickerProps = Omit<DatePickerProps<Dayjs, false>, 'value' | 'onChange' | 'onCalendarChange' | 'showTime'> & {
  value?: string
  onChange?: (value: string | undefined) => void
}

export function CalendarDayPicker({ value, onChange, ...props }: CalendarDayPickerProps) {
  function changeDay(date: Dayjs | null) {
    const day = date?.format(dayFormat)
    if (day !== value) onChange?.(day)
  }

  return (
    <CalendarPicker
      {...props}
      value={value ? dayjs(value) : null}
      onCalendarChange={date => { if (!Array.isArray(date)) changeDay(date) }}
      onChange={changeDay}
    />
  )
}
