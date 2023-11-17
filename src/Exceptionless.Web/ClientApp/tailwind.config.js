// Themes: https://github.com/saadeghi/daisyui/blob/master/src/theming/themes.js

/** @type {import('tailwindcss').Config} */
export default {
	content: ['./src/**/*.{html,js,svelte,ts}'],
	theme: {
		extend: {
			colors: {
				primary: '#6ebc1a',
				secondary: '#2c2c2c',
				accent: '#84cc16',
				neutral: '#545454',
				'base-100': '#fafafa',
				info: '#6ebc1a',
				success: '#6ebc1a',
				warning: '#fa810b',
				error: '#bb423f'
			}
		}
	},
	plugins: ['flowbite/plugin'],
	darkMode: 'class'
};
