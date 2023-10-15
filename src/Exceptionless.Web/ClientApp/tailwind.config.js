// Themes: https://github.com/saadeghi/daisyui/blob/master/src/theming/themes.js

/** @type {import('tailwindcss').Config} */
export default {
	content: ['./src/**/*.{html,js,svelte,ts}'],
	theme: {
		extend: {}
	},
	plugins: [require('daisyui')],
	daisyui: {
		logs: false,
		themes: [
			{
				light: {
					primary: '#6ebc1a',
					secondary: '#2c2c2c',
					accent: '#84cc16',
					neutral: '#545454',
					'base-100': '#fafafa',
					info: '#6ebc1a',
					success: '#6ebc1a',
					warning: '#fa810b',
					error: '#bb423f',
					'--rounded-box': '0.25rem',
					'--rounded-btn': '0.125rem',
					'--rounded-badge': '0.125rem',
					'--animation-btn': '0',
					'--animation-input': '0',
					'--btn-focus-scale': '1',
					'--tab-radius': '0'
				},
				dark: {
					primary: '#5e9a00',
					secondary: '#2c2c2c',
					accent: '#84cc16',
					neutral: '#545454',
					'base-100': '#404040',
					info: '#6ebc1a',
					success: '#6ebc1a',
					warning: '#fa810b',
					error: '#bb423f',
					'--rounded-box': '0.25rem',
					'--rounded-btn': '0.125rem',
					'--rounded-badge': '0.125rem',
					'--animation-btn': '0',
					'--animation-input': '0',
					'--btn-focus-scale': '1',
					'--tab-radius': '0'
				}
			}
		]
	}
};
