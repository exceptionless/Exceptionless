/** @type {import('tailwindcss').Config} */
export default {
	content: ['./src/**/*.{html,js,svelte,ts}'],
	theme: {
		extend: {}
	},
	plugins: [require('daisyui')],
	daisyui: {
		themes: [
			{
				exceptionless: {
					primary: '#5e9a00',
					secondary: '#2c2c2c',
					accent: '#1fb2a6',
					neutral: '#545454',
					'base-100': '#f7f7f7',

					info: '#6ebc1a',
					success: '#6ebc1a',
					warning: '#fa810b',
					error: '#bb423f'
				}
			}
		]
	}
};
